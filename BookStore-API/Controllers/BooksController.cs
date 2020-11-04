using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using BookStore_API.Contracts;
using BookStore_API.Data;
using BookStore_API.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BookStore_API.Controllers
{
    /// <summary>
    /// Interacts with the books table
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class BooksController : ControllerBase
    {
        private readonly IBookRepository _bookRepository;
        private readonly ILoggerService _logger;
        private readonly IMapper _mapper;
        private readonly IWebHostEnvironment _env;
        public BooksController(IBookRepository bookRepository,
            ILoggerService logger,
            IMapper mapper,
            IWebHostEnvironment env)
        {
            _bookRepository = bookRepository;
            _logger = logger;
            _mapper = mapper;
            _env = env;
        }

        private string GetImagePath(string fileName) 
            => (System.IO.Path.Combine(_env.ContentRootPath, "Uploads", fileName));

        /// <summary>
        /// Gets all books
        /// </summary>
        /// <returns>A list of books</returns>
        [HttpGet]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetBooks()
        {
            var location = GetControllerActionNames();
            try
            {
                _logger.LogInfo($"{location}: Attempted Call");
                var books = await _bookRepository.FindAll();
                var response = _mapper.Map<IList<BookDTO>>(books);
                foreach(var item in response)
                {
                    if (!string.IsNullOrEmpty(item.Image))
                    {
                        var imgPath = GetImagePath(item.Image);
                        if (System.IO.File.Exists(imgPath))
                        {
                            byte[] imgBytes = System.IO.File.ReadAllBytes(imgPath);
                            item.File = Convert.ToBase64String(imgBytes);
                        }
                    }
                }
                _logger.LogInfo($"{location}: Successful");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return InternalError($"{location}: {ex.Message} - {ex.InnerException}");
            }
        }

        /// <summary>
        /// Gets a book by ID
        /// </summary>
        /// <param name="id"></param>
        /// <returns>Book record</returns>
        [HttpGet("{id:int}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetBook(int id)
        {
            var location = GetControllerActionNames();
            try
            {
                _logger.LogInfo($"{location}: Attempted Call for id: {id}");
                var book = await _bookRepository.FindById(id);
                if (book == null)
                {
                    _logger.LogWarn($"{location}: Failed to retrieve record with id: {id}");
                    return NotFound();
                }
                var response = _mapper.Map<BookDTO>(book);
                if (!string.IsNullOrEmpty(response.Image))
                {
                    var imgPath = GetImagePath(response.Image);
                    if (System.IO.File.Exists(imgPath))
                    {
                        byte[] imgBytes = System.IO.File.ReadAllBytes(imgPath);
                        response.File = Convert.ToBase64String(imgBytes);
                    }
                }

                _logger.LogInfo($"{location}: Successful got record with id: {id}");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return InternalError($"{location}: {ex.Message} - {ex.InnerException}");
            }
        }

        /// <summary>
        /// Creates a new book
        /// </summary>
        /// <param name="bookDTO">Book</param>
        /// <returns>Book Object</returns>
        [HttpPost]
        [Authorize(Roles = "Administrator")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Create([FromBody] BookCreateDTO bookDTO)
        {
            var location = GetControllerActionNames();
            try
            {
                _logger.LogInfo($"{location}: Create Attempted");
                if (bookDTO == null)
                {
                    _logger.LogWarn($"{location}: Empty Request was submitted");
                    return BadRequest(ModelState);
                }
                if (!ModelState.IsValid)
                {
                    _logger.LogWarn($"{location}: Data was Incomplete");
                    return BadRequest(ModelState);
                }
                var book = _mapper.Map<Book>(bookDTO);
                var isSuccess = await _bookRepository.Create(book);
                if (!isSuccess)
                {
                    return InternalError($"{location}: Creation failed");
                }

                if (!string.IsNullOrEmpty(bookDTO.File)) 
                {
                    var imgPath = GetImagePath(bookDTO.Image);
                    byte[] imageData = Convert.FromBase64String(bookDTO.File);
                    System.IO.File.WriteAllBytes(imgPath, imageData);
                }

                _logger.LogInfo($"{location}: Creation was successful");
                _logger.LogInfo($"{location}: {book}");
                return Created("Create", new { book });
            }
            catch (Exception ex)
            {
                return InternalError($"{location}: {ex.Message} - {ex.InnerException}");
            }
        }

        /// <summary>
        /// Updates a book
        /// </summary>
        /// <param name="id"></param>
        /// <param name="bookDTO">Book</param>
        /// <returns></returns>
        [HttpPut("{id}")]
        [Authorize(Roles = "Administrator")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Update(int id, [FromBody] BookUpdateDTO bookDTO)
        {
            var location = GetControllerActionNames();
            try
            {
                _logger.LogInfo($"{location}: Update attempted on record with id: {id}");
                if (id < 0 || bookDTO == null || id != bookDTO.Id)
                {
                    _logger.LogWarn($"{location}: Update failed with bad data - id {id}");
                    return BadRequest();
                }
                
                var isExists = await _bookRepository.isExists(id);
                if (!isExists)
                {
                    _logger.LogWarn($"{location}: Failed to retrieve record with id: {id}");
                    return NotFound();
                }
                
                if (!ModelState.IsValid)
                {
                    _logger.LogWarn($"{location}: Data was incomplete");
                    return BadRequest(ModelState);
                }
                var oldImage = await _bookRepository.GetImageFileName(id);
                var book = _mapper.Map<Book>(bookDTO);
                var isSuccess = await _bookRepository.Update(book);
                if (!isSuccess)
                {
                    return InternalError($"Update failed for record with id: {id}");
                }

                if (!bookDTO.Image.Equals(oldImage))
                {
                    if (System.IO.File.Exists(GetImagePath(oldImage)))
                    {
                        System.IO.File.Delete(GetImagePath(oldImage));
                    }
                }

                if (!string.IsNullOrEmpty(bookDTO.File))
                {
                    byte[] imageBytes = Convert.FromBase64String(bookDTO.File);
                    System.IO.File.WriteAllBytes(GetImagePath(bookDTO.Image), imageBytes);
                }

                _logger.LogInfo($"{location}: Record with id: {id} successfully updated");
                return NoContent();
            }
            catch (Exception ex)
            {
                return InternalError($"{location}: {ex.Message} - {ex.InnerException}");
            }
        }

        /// <summary>
        /// Removes a book by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrator")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> Delete(int id)
        {
            var location = GetControllerActionNames();
            try
            {
                _logger.LogInfo($"{location}: Delete attempted on record with id: {id}");
                if(id < 1)
                {
                    _logger.LogWarn($"{location}: Delete failed with bad data - id {id}");
                    return BadRequest();
                }
                var isExists = await _bookRepository.isExists(id);
                if (!isExists)
                {
                    _logger.LogWarn($"{location}: Failed to retrieve record with id: {id}");
                    return NotFound();
                }

                var oldImage = await _bookRepository.GetImageFileName(id);
                var book = await _bookRepository.FindById(id);
                var isSuccess = await _bookRepository.Delete(book);

                if (!isSuccess)
                {
                    return InternalError($"{location}: Delete failed for record with id: {id}");
                }

                if (System.IO.File.Exists(GetImagePath(oldImage)))
                {
                    System.IO.File.Delete(GetImagePath(oldImage));
                }

                _logger.LogInfo($"{location}: Record with id: {id} successfully deleted");
                return NoContent();
            }
            catch (Exception ex)
            {
                return InternalError($"{location}: {ex.Message} - {ex.InnerException}");
            }
        }

        private string GetControllerActionNames()
        {
            var controller = ControllerContext.ActionDescriptor.ControllerName;
            var actionName = ControllerContext.ActionDescriptor.ActionName;
            return ($"{controller} - {actionName}");
        }

        private ObjectResult InternalError(string message)
        {
            _logger.LogError(message);
            return StatusCode(500, "Something went wrong.  Please contact the Administrator.");
        }
    }
}
