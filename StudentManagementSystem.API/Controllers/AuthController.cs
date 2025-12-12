using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using StudentManagementSystem.BusinessLayer.DTOs.AuthDTOs;
using StudentManagementSystem.BusinessLayer.Services;
using StudentManagementSystem.DAL.Entities;
using System.Runtime.ConstrainedExecution;
using System.Security.Claims;

namespace StudentManagementSystem.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<BaseUser> userManager;
        private readonly SignInManager<BaseUser> signInManager;
        private readonly RoleManager<IdentityRole> roleManager;
        private readonly TokenService tokenService;

        public AuthController(
            UserManager<BaseUser> userManager,
            SignInManager<BaseUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            TokenService tokenService)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
            this.roleManager = roleManager;
            this.tokenService = tokenService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDTO dto)
        {
            if (!await roleManager.RoleExistsAsync(dto.Role))
                return BadRequest("Role does not exist");

            var user = new BaseUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                FullName = dto.FullName,
                Address = dto.Address,
                Role = dto.Role
            };

            var result = await userManager.CreateAsync(user, dto.Password);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            await userManager.AddToRoleAsync(user, dto.Role);

            return Ok("User registered successfully");
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDTO dto)
        {
            var user = await userManager.FindByEmailAsync(dto.Email);
            if (user == null) 
                return Unauthorized("Invalid credentials");

            var result = await signInManager.CheckPasswordSignInAsync(user, dto.Password, false);
            if (!result.Succeeded) return Unauthorized("Invalid credentials");

            var roles = await userManager.GetRolesAsync(user);
            var token = tokenService.GenerateToken(user, roles);

            return Ok(new { token });
        }

        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            if (userId == null)
                return Unauthorized();

            var user = await userManager.FindByIdAsync(userId);

            return Ok(new
            {
                user.FullName,
                user.Email
            });
        }
    }
}
