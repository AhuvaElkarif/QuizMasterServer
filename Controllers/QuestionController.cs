using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using QuizMasterServer.Models;
using QuizMasterServer.Services;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ExamManagementMongoApi.Controllers
{
    [ApiController]
    [Route("api/exam/{examId:length(24)}/[controller]")]
    [Authorize(Policy = "TeacherOnly")]
    public class QuestionController : ControllerBase
    {
        private readonly IQuestionService _questionService;

        public QuestionController(IQuestionService questionService)
        {
            _questionService = questionService;
        }

        private ObjectId GetCurrentUserId()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return string.IsNullOrEmpty(userIdStr) ? ObjectId.Empty : ObjectId.Parse(userIdStr);
        }

        /// <summary>
        /// Get all questions for a specific exam
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<Question>>> GetQuestions(string examId)
        {
            try
            {
                var teacherId = GetCurrentUserId();
                if (teacherId == ObjectId.Empty)
                    return Unauthorized();

                var questions = await _questionService.GetQuestionsAsync(examId, teacherId);
                return Ok(questions);
            }
            catch (UnauthorizedAccessException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Get a specific question
        /// </summary>
        [HttpGet("{id:length(24)}")]
        public async Task<ActionResult<Question>> GetQuestion(string examId, string id)
        {
            try
            {
                var teacherId = GetCurrentUserId();
                if (teacherId == ObjectId.Empty)
                    return Unauthorized();

                var question = await _questionService.GetQuestionAsync(examId, id, teacherId);
                return Ok(question);
            }
            catch (UnauthorizedAccessException ex)
            {
                return NotFound(ex.Message);
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

        /// <summary>
        /// Create a new question for an exam
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<Question>> CreateQuestion(string examId, [FromBody] Question question)
        {
            try
            {
                var teacherId = GetCurrentUserId();
                if (teacherId == ObjectId.Empty)
                    return Unauthorized();

                var createdQuestion = await _questionService.CreateQuestionAsync(examId, question, teacherId);
                return CreatedAtAction(nameof(GetQuestion), new { examId, id = createdQuestion.Id }, createdQuestion);
            }
            catch (UnauthorizedAccessException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Update an existing question
        /// </summary>
        [HttpPut("{id:length(24)}")]
        public async Task<IActionResult> UpdateQuestion(string examId, string id, [FromBody] Question updatedQuestion)
        {
            try
            {
                var teacherId = GetCurrentUserId();
                if (teacherId == ObjectId.Empty)
                    return Unauthorized();

                await _questionService.UpdateQuestionAsync(examId, id, updatedQuestion, teacherId);
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                return NotFound(ex.Message);
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
        /// Delete a question
        /// </summary>
        [HttpDelete("{id:length(24)}")]
        public async Task<IActionResult> DeleteQuestion(string examId, string id)
        {
            try
            {
                var teacherId = GetCurrentUserId();
                if (teacherId == ObjectId.Empty)
                    return Unauthorized();

                await _questionService.DeleteQuestionAsync(examId, id, teacherId);
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                return NotFound(ex.Message);
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