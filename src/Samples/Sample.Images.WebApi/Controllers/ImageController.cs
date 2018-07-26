﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Sample.Images.FileStore;
using Sample.Images.Messages;
using SlimMessageBus;

namespace Sample.Images.WebApi.Controllers
{
    [Route("api/[controller]")]
    public class ImageController : Controller
    {
        private readonly IRequestResponseBus _bus;
        private readonly IFileStore _fileStore;
        private readonly IThumbnailFileIdStrategy _fileIdStrategy;

        public ImageController(IRequestResponseBus bus, IFileStore fileStore, IThumbnailFileIdStrategy fileIdStrategy)
        {
            _bus = bus;
            _fileStore = fileStore;
            _fileIdStrategy = fileIdStrategy;
        }

        [HttpGet("{fileId}/r")]
        public async Task<ActionResult> GetImageThumbnail(string fileId, [FromQuery] ThumbnailMode mode, [FromQuery] int w, [FromQuery] int h, CancellationToken cancellationToken)
        {
            var thumbFileId = _fileIdStrategy.GetFileId(fileId, w, h, mode);

            var thumbFileContent = await _fileStore.GetFile(thumbFileId).ConfigureAwait(false);
            if (thumbFileContent == null)
            {
                try
                {
                    var thumbGenResponse = await _bus.Send(new GenerateThumbnailRequest(fileId, mode, w, h), cancellationToken).ConfigureAwait(false);
                    thumbFileContent = await _fileStore.GetFile(thumbGenResponse.FileId).ConfigureAwait(false);
                }
                catch (RequestHandlerFaultedMessageBusException)
                {
                    // The request handler for GenerateThumbnailRequest failed
                    return NotFound();
                }
                catch (OperationCanceledException)
                {
                    // The request was cancelled (HTTP connection cancelled, or request timed out)
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "The request was cancelled");
                }
            }

            return ServeStream(thumbFileContent);
        }

        [HttpGet("{fileId}")]
        public async Task<ActionResult> GetImage(string fileId)
        {
            var fileContent = await _fileStore.GetFile(fileId).ConfigureAwait(false);
            if (fileContent == null)
            {
                return NotFound();
            }
            return ServeStream(fileContent);
        }

        public static FileStreamResult ServeStream(Stream content)
        {
            // ToDo: determine media type 
            var r = new FileStreamResult(content, "image/jpeg");
            // ToDo: add cache-control headers
            return r;
        }
    }
}