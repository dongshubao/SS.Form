﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using System.Web.Http;
using SiteServer.Plugin;
using SS.Form.Core;
using SS.Form.Core.Box;
using SS.Form.Core.Utils;

namespace SS.Form.Controllers.Pages
{
    [RoutePrefix("pages/fields")]
    public class PagesFieldsController : ApiController
    {
        private const string Route = "";
        private const string RouteExport = "actions/export";
        private const string RouteImport = "actions/import";

        [HttpGet, Route(Route)]
        public IHttpActionResult Get()
        {
            try
            {
                var request = Context.AuthenticatedRequest;

                var formInfo = FormManager.GetFormInfoByGet(request);
                if (formInfo == null) return NotFound();
                if (!request.IsAdminLoggin || !request.AdminPermissions.HasSitePermissions(formInfo.SiteId, FormUtils.MenuFormsPermission)) return Unauthorized();

                var adminToken = Context.AdminApi.GetAccessToken(request.AdminId, request.AdminName, TimeSpan.FromDays(1));

                var list = new List<object>();
                foreach (var fieldInfo in FieldManager.GetFieldInfoList(formInfo.Id))
                {
                    list.Add(new
                    {
                        fieldInfo.Id,
                        fieldInfo.Title,
                        InputType = FieldManager.GetFieldTypeText(fieldInfo.FieldType),
                        fieldInfo.Validate,
                        fieldInfo.Taxis
                    });
                }

                return Ok(new
                {
                    Value = list,
                    AdminToken = adminToken
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpDelete, Route(Route)]
        public IHttpActionResult Delete()
        {
            try
            {
                var request = Context.AuthenticatedRequest;

                var formInfo = FormManager.GetFormInfoByGet(request);
                if (formInfo == null) return NotFound();
                if (!request.IsAdminLoggin || !request.AdminPermissions.HasSitePermissions(formInfo.SiteId, FormUtils.MenuFormsPermission)) return Unauthorized();

                var fieldId = request.GetQueryInt("fieldId");
                FieldManager.Repository.Delete(formInfo.Id, fieldId);
                FieldManager.ClearCache(formInfo.Id);

                var list = new List<object>();
                foreach (var fieldInfo in FieldManager.GetFieldInfoList(formInfo.Id))
                {
                    list.Add(new
                    {
                        fieldInfo.Id,
                        fieldInfo.Title,
                        InputType = FieldManager.GetFieldTypeText(fieldInfo.FieldType),
                        fieldInfo.Validate,
                        fieldInfo.Taxis
                    });
                }

                return Ok(new
                {
                    Value = list
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpPost, Route(RouteExport)]
        public IHttpActionResult Export()
        {
            try
            {
                var request = Context.AuthenticatedRequest;

                var formInfo = FormManager.GetFormInfoByPost(request);
                if (formInfo == null) return NotFound();
                if (!request.IsAdminLoggin || !request.AdminPermissions.HasSitePermissions(formInfo.SiteId, FormUtils.MenuFormsPermission)) return Unauthorized();

                //var fileName = FieldManager.Export(formInfo.Id);\

                var fileName = "表单字段.zip";
                var filePath = Context.UtilsApi.GetTemporaryFilesPath(fileName);
                var directoryPath = Context.UtilsApi.GetTemporaryFilesPath("FormFields");

                FormUtils.DeleteDirectoryIfExists(directoryPath);
                FormUtils.CreateDirectoryIfNotExists(directoryPath);

                FormBox.ExportFields(formInfo.Id, directoryPath);

                Context.UtilsApi.CreateZip(filePath, directoryPath);

                var url = Context.UtilsApi.GetRootUrl($"SiteFiles/TemporaryFiles/{fileName}");

                return Ok(new
                {
                    Value = url
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpPost, Route(RouteImport)]
        public IHttpActionResult Import()
        {
            try
            {
                var request = Context.AuthenticatedRequest;

                var formInfo = FormManager.GetFormInfoByGet(request);
                if (formInfo == null) return NotFound();
                if (!request.IsAdminLoggin || !request.AdminPermissions.HasSitePermissions(formInfo.SiteId, FormUtils.MenuFormsPermission)) return Unauthorized();

                foreach (string name in HttpContext.Current.Request.Files)
                {
                    var postFile = HttpContext.Current.Request.Files[name];

                    if (postFile == null)
                    {
                        return BadRequest("Could not read zip from body");
                    }
                    
                    var filePath = Context.UtilsApi.GetTemporaryFilesPath("form.zip");
                    FormUtils.DeleteFileIfExists(filePath);

                    if (!FormUtils.EqualsIgnoreCase(Path.GetExtension(postFile.FileName), ".zip"))
                    {
                        return BadRequest("zip file extension is not correct");
                    }

                    postFile.SaveAs(filePath);

                    var directoryPath = Context.UtilsApi.GetTemporaryFilesPath("form");
                    FormUtils.DeleteDirectoryIfExists(directoryPath);
                    Context.UtilsApi.ExtractZip(filePath, directoryPath);

                    var isHistoric = FormBox.IsHistoric(directoryPath);
                    FormBox.ImportFields(formInfo.SiteId, formInfo.Id, directoryPath, isHistoric);

                    //FieldManager.Import(formInfo.SiteId, formInfo.Id, filePath);
                }

                return Ok(new
                {
                    Value = true
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }
}
