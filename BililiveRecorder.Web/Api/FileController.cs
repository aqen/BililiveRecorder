using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BililiveRecorder.Core;
using BililiveRecorder.Web.Models.Rest.Files;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;

namespace BililiveRecorder.Web.Api
{
    [ApiController, Route("api/[controller]", Name = "[controller] [action]")]
    public sealed class FileController : ControllerBase, IDisposable
    {
        private readonly PhysicalFileProvider? fileProvider;
        private bool disposedValue;

        public FileController(IRecorder recorder, BililiveRecorderFileExplorerSettings fileExplorerSettings)
        {
            if (recorder is null) throw new ArgumentNullException(nameof(recorder));
            if (fileExplorerSettings is null) throw new ArgumentNullException(nameof(fileExplorerSettings));
            if (fileExplorerSettings.Enable)
            {
                this.fileProvider = new PhysicalFileProvider(recorder.Config.Global.WorkDirectory!);
            }
        }

        /// <summary>
        /// 获取录播目录文件信息
        /// </summary>
        /// <param name="path" example="/">路径</param>
        /// <returns></returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public FileApiResult GetFiles([FromQuery] string path)
        {
            if (this.fileProvider is null || path is null)
                return FileApiResult.NotExist;

            var contents = this.fileProvider.GetDirectoryContents(path);

            if (!contents.Exists)
                return FileApiResult.NotExist;

            var fileLikes = new List<FileLikeDto>();

            foreach (var content in contents)
            {
                try
                {
                    if (!content.Exists)
                        continue;

                    if (content.IsDirectory)
                    {
                        fileLikes.Add(new FolderDto
                        {
                            Name = content.Name,
                            LastModified = content.LastModified,
                        });
                    }
                    else
                    {
                        var pathTrimmed = path.Trim('/');
                        fileLikes.Add(new FileDto
                        {
                            Name = content.Name,
                            LastModified = content.LastModified,
                            Size = content.Length,

                            // Path.Combine 在 Windows 上会用 \
                            Url = "/file/" + (pathTrimmed.Length > 0 ? pathTrimmed + '/' : string.Empty) + content.Name,
                        });
                    }
                }
                catch (Exception) { }
            }

            return new FileApiResult(true, path, fileLikes);
        }
        /// <summary>
        /// 删除录播目录中的文件或文件夹
        /// 仅适配windows/Linux
        /// </summary>
        /// <param name="del" example="/example.txt">文件路径或文件夹路径</param>
        /// <returns></returns>
        [HttpDelete]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<FileApiResult> DeleteFile([FromQuery] string del)
        {
            if (this.fileProvider is null || del is null)
            {
                return new FileApiResult(false, del ?? "", "path not found");
            }


            if (this.fileProvider.Root == null)
            {
                return new FileApiResult(false, this.fileProvider.Root ?? "", "work directory not found");
            }

	    // 判断具体平台
	    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
	    {
		del = del.Replace('/', '\\');

	    }

            var path = this.fileProvider.Root+del;
            var fileInfo = this.fileProvider.GetFileInfo(del);

            if (fileInfo.Exists)
            {
                // 处理文件删除
                if (fileInfo.IsDirectory)
                {
                    try
                    {
                        System.IO.Directory.Delete(path, true); // 递归删除文件夹及其内容
                        return new FileApiResult(true, del, "directory deleted");
                    }
                    catch (Exception ex)
                    {
                        return new FileApiResult(false, del, $"删除文件夹失败: {ex.Message}");
                    }
                }
                else
                {
                    try
                    {
                        System.IO.File.Delete(path);
                        return new FileApiResult(true, del, "file deleted");
                    }
                    catch (Exception ex)
                    {
                        return new FileApiResult(false, del, $"删除文件失败: {ex.Message}");
                    }
                }
            }
            else
            {
                try
                {
                    System.IO.Directory.Delete(path, true); // 递归删除文件夹及其内容
                    return new FileApiResult(true, del, "directory deleted");
                }
                catch (Exception ex)
                {
                    return new FileApiResult(false, del, $"删除文件夹失败: {ex.Message}");
                }
            }
        }

        private void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.fileProvider?.Dispose();
                }
                this.disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
