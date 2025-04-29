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
    [Route("questions")]
    [Authorize(Roles = "Teacher")]
    public class QuestionsController : ControllerBase
    {
        private readonly QuestionService _questionService;

        public QuestionsController(QuestionService questionService)
        {
            _questionService = questionService;
        }

        [HttpGet]
        public async Task<ActionResult<List<Question>>> GetAll()
        {
            var questions = await _questionService.GetAllAsync();
            return Ok(questions);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Question>> GetById(string id)
        {
            var question = await _questionService.GetByIdAsync(id);

            if (question == null)
            {
                return NotFound();
            }

            return Ok(question);
        }

        [HttpPost]
        public async Task<ActionResult<Question>> Create([FromBody] QuestionCreateRequest questionRequest)
        {
            var teacherId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var question = await _questionService.CreateAsync(questionRequest, teacherId);
            return CreatedAtAction(nameof(GetById), new { id = question.Id }, question);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<Question>> Update(string id, [FromBody] QuestionUpdateRequest questionRequest)
        {
            try
            {
                var question = await _questionService.UpdateAsync(id, questionRequest);
                return Ok(question);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(string id)
        {
            await _questionService.DeleteAsync(id);
            return NoContent();
        }
    }
}