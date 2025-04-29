
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using QuizMaster.Services;
using Swashbuckle.AspNetCore.Annotations;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using QuizMaster.Models;
using QuizMaster.DTO;

namespace QuizMaster.Controllers
{
    [ApiController]
    [Route("quiz")]
    [Authorize]
    public class QuizController : ControllerBase
    {
        private readonly QuizService _quizService;

        public QuizController(QuizService quizService)
        {
            _quizService = quizService;
        }

        [HttpGet("random")]
        [Authorize(Roles = "Student")]
        public async Task<ActionResult<Quiz>> GetRandomQuiz([FromQuery] int count = 10)
        {
            var studentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var quiz = await _quizService.CreateRandomQuizAsync(studentId, count);

            // Remove correct answers from response
            foreach (var question in quiz.Questions)
            {
                question.CorrectAnswer = null;
            }

            return Ok(quiz);
        }

        [HttpPost("{quizId}/submit")]
        [Authorize(Roles = "Student")]
        public async Task<ActionResult<QuizResult>> SubmitQuiz(string quizId, [FromBody] QuizSubmitRequest submitRequest)
        {
            try
            {
                var studentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var result = await _quizService.SubmitQuizAsync(quizId, studentId, submitRequest.Answers);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{quizId}/results")]
        [Authorize]
        public async Task<ActionResult<QuizResult>> GetQuizResults(string quizId)
        {
            var result = await _quizService.GetQuizResultAsync(quizId);

            if (result == null)
            {
                return NotFound();
            }

            // If student, verify it's their quiz
            if (User.IsInRole("Student"))
            {
                var studentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (result.StudentId != studentId)
                {
                    return Forbid();
                }
            }

            return Ok(result);
        }

        [HttpGet("my-results")]
        [Authorize(Roles = "Student")]
        public async Task<ActionResult<List<QuizResult>>> GetStudentResults()
        {
            var studentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var results = await _quizService.GetStudentResultsAsync(studentId);
            return Ok(results);
        }
    }
}