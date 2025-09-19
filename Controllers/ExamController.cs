using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using QuizMasterServer.DTOs;
using QuizMasterServer.Services;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace QuizMasterServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "TeacherOrStudent")]
    public class ExamController : ControllerBase
    {
        private readonly IExamService _examService;

        public ExamController(IExamService examService)
        {
            _examService = examService;
        }

        private ObjectId GetCurrentUserId()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !ObjectId.TryParse(userIdStr, out var objectId))
            {
                return ObjectId.Empty;
            }
            return objectId;
        }

        /// <summary>
        /// Get all exams for the current teacher
        /// </summary>
        [HttpGet]
        [Authorize(Policy = "TeacherOnly")]
        public async Task<ActionResult<List<ExamDto>>> GetMyExams()
        {
            try
            {
                var teacherId = GetCurrentUserId();
                if (teacherId == ObjectId.Empty)
                    return Unauthorized();

                var exams = await _examService.GetMyExamsAsync(teacherId);
                return Ok(exams);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Get a specific exam with questions
        /// </summary>
        [HttpGet("{id:length(24)}")]
        public async Task<ActionResult<ExamWithQuestionsDto>> GetExam(string id)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == ObjectId.Empty)
                    return Unauthorized();

                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

                var exam = await _examService.GetExamAsync(id, userId, userRole);
                if (exam == null)
                    return NotFound();

                return Ok(exam);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Create a new exam
        /// </summary>
        [HttpPost]
        [Authorize(Policy = "TeacherOnly")]
        public async Task<ActionResult<ExamDto>> CreateExam([FromBody] ExamCreateDto examCreateDto)
        {
            try
            {
                var teacherId = GetCurrentUserId();
                if (teacherId == ObjectId.Empty)
                    return Unauthorized();

                var dto = await _examService.CreateExamAsync(examCreateDto, teacherId);
                return CreatedAtAction(nameof(GetExam), new { id = dto.Id }, dto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Update an existing exam
        /// </summary>
        [HttpPut("{id:length(24)}")]
        [Authorize(Policy = "TeacherOnly")]
        public async Task<IActionResult> UpdateExam(string id, [FromBody] ExamUpdateDto updatedExamDto)
        {
            try
            {
                var teacherId = GetCurrentUserId();
                if (teacherId == ObjectId.Empty)
                    return Unauthorized();

                await _examService.UpdateExamAsync(id, updatedExamDto, teacherId);
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
        /// Delete an exam
        /// </summary>
        [HttpDelete("{id:length(24)}")]
        [Authorize(Policy = "TeacherOnly")]
        public async Task<IActionResult> DeleteExam(string id)
        {
            try
            {
                var teacherId = GetCurrentUserId();
                if (teacherId == ObjectId.Empty)
                    return Unauthorized();

                await _examService.DeleteExamAsync(id, teacherId);
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
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }
    }
}