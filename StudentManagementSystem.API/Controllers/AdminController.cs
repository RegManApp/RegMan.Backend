using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using StudentManagementSystem.BusinessLayer.DTOs.Auth;
using StudentManagementSystem.DAL.Entities;

namespace StudentManagementSystem.API.Controllers
{
    [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("api/admin")]
    public class AdminController : ControllerBase
    {
        private readonly UserManager<BaseUser> _userManager;
        private readonly RoleManager<IdentityRole> roleManager;

        public AdminController(UserManager<BaseUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            this.roleManager = roleManager;

        }

        [HttpPost("create-user")]
        public async Task<IActionResult> CreateUser(CreateUserDTO dto)
        {
            if (!await roleManager.RoleExistsAsync(dto.Role))
                return BadRequest("Role does not exist");

            var user = new BaseUser
            {
                FullName = dto.FullName,
                Email = dto.Email,
                UserName = dto.Email,
                Address = dto.Address,
                Role = dto.Role
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            await _userManager.AddToRoleAsync(user, dto.Role);

            return Ok("User created successfully");
        }
    }
}
