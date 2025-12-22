using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RegMan.Backend.API.Common;
using RegMan.Backend.DAL.DataContext;
using RegMan.Backend.DAL.Entities;
using System.Security.Claims;

namespace RegMan.Backend.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly AppDbContext _context;

        public NotificationController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Get all notifications for the current user
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetNotifications(
            [FromQuery] bool? unreadOnly = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse<string>.FailureResponse(
                        "User not authenticated or userId missing.",
                        StatusCodes.Status401Unauthorized
                    ));
                }

                var query = _context.Notifications
                    .Where(n => n.UserId == userId);

                if (unreadOnly == true)
                    query = query.Where(n => !n.IsRead);

                var totalCount = await query.CountAsync();
                var unreadCount = await _context.Notifications
                    .CountAsync(n => n.UserId == userId && !n.IsRead);

                var notifications = await query
                    .OrderByDescending(n => n.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(n => new
                    {
                        n.NotificationId,
                        n.Type,
                        n.Title,
                        n.Message,
                        n.EntityType,
                        n.EntityId,
                        n.IsRead,
                        n.ReadAt,
                        n.CreatedAt
                    })
                    .ToListAsync();

                var payload = new
                {
                    notifications,
                    totalCount,
                    unreadCount,
                    page,
                    pageSize,
                    totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };

                return Ok(ApiResponse<object>.SuccessResponse(payload));
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<string>.FailureResponse(
                        "Failed to load notifications.",
                        StatusCodes.Status500InternalServerError
                    )
                );
            }
        }

        /// <summary>
        /// Get unread notification count
        /// </summary>
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(userId))
                    return Ok(ApiResponse<object>.SuccessResponse(new { count = 0 }));

                var count = await _context.Notifications
                    .Where(n => n.UserId == userId && !n.IsRead)
                    .CountAsync();

                return Ok(ApiResponse<object>.SuccessResponse(new { count }));
            }
            catch
            {
                return Ok(ApiResponse<object>.SuccessResponse(new { count = 0 }));
            }
        }

        /// <summary>
        /// Mark a notification as read
        /// </summary>
        [HttpPost("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == userId);

            if (notification == null)
                return NotFound(ApiResponse<string>.FailureResponse(
                    "Notification not found",
                    StatusCodes.Status404NotFound
                ));

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return Ok(ApiResponse<string>.SuccessResponse("Notification marked as read"));
        }

        /// <summary>
        /// Mark all notifications as read
        /// </summary>
        [HttpPost("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var unreadNotifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            foreach (var notification in unreadNotifications)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.SuccessResponse($"Marked {unreadNotifications.Count} notifications as read"));
        }

        /// <summary>
        /// Delete a notification
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNotification(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == userId);

            if (notification == null)
                return NotFound(ApiResponse<string>.FailureResponse(
                    "Notification not found",
                    StatusCodes.Status404NotFound
                ));

            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.SuccessResponse("Notification deleted"));
        }

        /// <summary>
        /// Delete all read notifications
        /// </summary>
        [HttpDelete("clear-read")]
        public async Task<IActionResult> ClearReadNotifications()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var readNotifications = await _context.Notifications
                .Where(n => n.UserId == userId && n.IsRead)
                .ToListAsync();

            _context.Notifications.RemoveRange(readNotifications);
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.SuccessResponse($"Deleted {readNotifications.Count} notifications"));
        }
    }
}
