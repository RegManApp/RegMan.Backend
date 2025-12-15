using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentManagementSystem.BusinessLayer.Contracts;
using StudentManagementSystem.BusinessLayer.DTOs.InstructorDTOs;

namespace StudentManagementSystem.API.Controllers;

[ApiController]
[Route("api/instructor")]
[Authorize(Roles = "Admin")]
public class InstructorController : ControllerBase
{
    private readonly IInstructorService instructorService;

    public InstructorController(IInstructorService instructorService)
    {
        this.instructorService = instructorService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateInstructorDTO dto)
        => Ok(await instructorService.CreateAsync(dto));

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await instructorService.GetAllAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
        => Ok(await instructorService.GetByIdAsync(id));

    [HttpGet("{id}/schedule")]
    public async Task<IActionResult> GetSchedule(int id)
        => Ok(await instructorService.GetScheduleAsync(id));
}
