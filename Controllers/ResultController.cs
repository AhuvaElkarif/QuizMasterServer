using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using QuizMasterServer.DTOs;
using QuizMasterServer.Services;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ExamManagementMongoApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "TeacherOnly")]
    public class ResultController : ControllerBase
    {
        private readonly IResultService _resultService;

        public ResultController(IResultService resultService)
        {
            _resultService = resultService;
        }

        private ObjectId GetCurrentUserId()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return string.IsNullOrEmpty(userIdStr) ? ObjectId.Empty : ObjectId.Parse(userIdStr);
        }

        /// <summary>
        /// Get all ungraded results for the current teacher's exams
        /// </summary>
        [HttpGet("ungraded")]
        public async Task<IActionResult> GetUngradedResults()
        {
            try
            {
                var teacherId = GetCurrentUserId();
                if (teacherId == ObjectId.Empty)
                    return Unauthorized();

                var results = await _resultService.GetUngradedResultsAsync(teacherId);
                return Ok(results);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        public class FeedbackDto
        {
            public string Feedback { get; set; }
        }

        /// <summary>
        /// Update feedback for a specific result
        /// </summary>
        [HttpPut("{id:length(24)}/feedback")]
        public async Task<IActionResult> UpdateFeedback(string id, FeedbackDto dto)
        {
            try
            {
                var teacherId = GetCurrentUserId();
                if (teacherId == ObjectId.Empty)
                    return Unauthorized();

                await _resultService.UpdateFeedbackAsync(id, dto.Feedback, teacherId);
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(500, ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Get analytics data for the teacher's exams
        /// </summary>
        [HttpGet("analytics")]
        public async Task<IActionResult> GetAnalytics()
        {
            try
            {
                var teacherId = GetCurrentUserId();
                if (teacherId == ObjectId.Empty)
                    return Unauthorized();

                var analytics = await _resultService.GetAnalyticsAsync(teacherId);
                return Ok(analytics);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }
    }
}