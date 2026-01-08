using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegMan.Backend.API.Common;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.Announcements;
using System.Security.Claims;

namespace RegMan.Backend.API.Controllers
{
    [Route("api/[controller]")]
    [Route("[controller]")]
    [ApiController]
    [Authorize]
    public class AnnouncementsController : ControllerBase
    {
        private readonly IAnnouncementsService announcementsService;

        public AnnouncementsController(IAnnouncementsService announcementsService)
        {
            this.announcementsService = announcementsService;
        }

        [HttpGet("mine")]
        public async Task<IActionResult> GetMine([FromQuery] bool includeArchived = false)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized(ApiResponse<string>.FailureResponse("Unauthorized", 401));

            var items = await announcementsService.GetMyAnnouncementsAsync(userId, includeArchived);
            return Ok(ApiResponse<object>.SuccessResponse(items));
        }

        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized(ApiResponse<string>.FailureResponse("Unauthorized", 401));

            var count = await announcementsService.GetMyUnreadCountAsync(userId);
            return Ok(ApiResponse<object>.SuccessResponse(new { count }));
        }

        [HttpPost("{announcementId:int}/read")]
        public async Task<IActionResult> MarkRead(int announcementId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized(ApiResponse<string>.FailureResponse("Unauthorized", 401));

            await announcementsService.MarkAnnouncementReadAsync(userId, announcementId);
            return Ok(ApiResponse<string>.SuccessResponse("OK"));
        }

        [HttpGet("scopes")]
        [Authorize(Roles = "Admin,Instructor")]
        public async Task<IActionResult> GetMyScopes()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
            if (userId == null)
                return Unauthorized(ApiResponse<string>.FailureResponse("Unauthorized", 401));

            var scopes = await announcementsService.GetMyScopesAsync(userId, role);
            return Ok(ApiResponse<object>.SuccessResponse(scopes));
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Instructor")]
        public async Task<IActionResult> Create([FromBody] CreateAnnouncementRequestDTO dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
            if (userId == null)
                return Unauthorized(ApiResponse<string>.FailureResponse("Unauthorized", 401));

            var result = await announcementsService.CreateAnnouncementAsync(userId, role, dto);
            return Ok(ApiResponse<object>.SuccessResponse(result, "Announcement sent"));
        }

        // Admin audit
        [HttpGet("audit")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Audit(
            [FromQuery] DateTime? fromUtc,
            [FromQuery] DateTime? toUtc,
            [FromQuery] string? createdByUserId,
            [FromQuery] bool? isArchived,
            [FromQuery] int? courseId,
            [FromQuery] int? sectionId,
            [FromQuery] string? targetRole)
        {
            var items = await announcementsService.GetAuditAsync(fromUtc, toUtc, createdByUserId, isArchived, courseId, sectionId, targetRole);
            return Ok(ApiResponse<object>.SuccessResponse(items));
        }

        [HttpPost("{announcementId:int}/archive")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Archive(int announcementId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized(ApiResponse<string>.FailureResponse("Unauthorized", 401));

            await announcementsService.ArchiveAnnouncementAsync(userId, announcementId);
            return Ok(ApiResponse<string>.SuccessResponse("Archived"));
        }
    }
}
