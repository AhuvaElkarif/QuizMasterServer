using QuizMasterServer.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using QuizMasterServer.DTOs;
using QuizMasterServer.Services;

namespace ExamManagementMongoApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "StudentOnly")]
    public class StudentController : ControllerBase
    {
        private readonly IStudentService _studentService;

        public StudentController(IStudentService studentService)
        {
            _studentService = studentService;
        }

        private ObjectId GetCurrentUserId()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return string.IsNullOrEmpty(userIdStr) ? ObjectId.Empty : ObjectId.Parse(userIdStr);
        }

        /// <summary>
        /// Get all available exams
        /// </summary>
        [HttpGet("exams")]
        public async Task<IActionResult> GetAvailableExams()
        {
            try
            {
                var exams = await _studentService.GetAvailableExamsAsync();
                return Ok(exams);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Start a new exam attempt
        /// </summary>
        [HttpPost("examattempt/start")]
        public async Task<IActionResult> StartExamAttempt([FromBody] StartExamAttemptDto dto)
        {
            try
            {
                var studentId = GetCurrentUserId();
                var result = await _studentService.StartExamAttemptAsync(dto.ExamId, studentId);

                if (!result.Success)
                {
                    if (result.Message.Contains("not found"))
                        return NotFound(result.Message);
                    if (result.Message.Contains("unfinished"))
                        return Conflict(result.Message);
                    return BadRequest(result.Message);
                }

                return Ok(result.Data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Submit answers for an exam attempt
        /// </summary>
        [HttpPost("examattempt/{attemptId:length(24)}/submit")]
        public async Task<IActionResult> SubmitAnswers(string attemptId, [FromBody] List<SubmitAnswerDto> submittedAnswersDto)
        {
            try
            {
                var studentId = GetCurrentUserId();
                var result = await _studentService.SubmitAnswersAsync(attemptId, submittedAnswersDto, studentId);

                if (!result.Success)
                {
                    if (result.Message.Contains("not found"))
                        return NotFound(result.Message);
                    if (result.Message.Contains("already submitted"))
                        return BadRequest(result.Message);
                    if (result.Message.Contains("invalid"))
                        return BadRequest(result.Message);
                    return BadRequest(result.Message);
                }

                return Ok(result.Data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Get all results for the current student
        /// </summary>
        [HttpGet("results")]
        public async Task<IActionResult> GetMyResults()
        {
            try
            {
                var studentId = GetCurrentUserId();
                var results = await _studentService.GetMyResultsAsync(studentId);
                return Ok(results);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Get answers for a specific exam attempt
        /// </summary>
        [HttpGet("{examAttemptId:length(24)}/answers")]
        public async Task<ActionResult<List<AnswerDto>>> GetAnswersForAttempt(string examAttemptId)
        {
            try
            {
                var answers = await _studentService.GetAnswersForAttemptAsync(examAttemptId);
                return Ok(answers);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }
    }
}