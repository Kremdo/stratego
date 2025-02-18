﻿using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Stratego.Api.Authorization.Contracts;
using Stratego.Api.Models;
using Stratego.Domain;

namespace Stratego.Api.Controllers
{
    //DO NOT TOUCH THIS FILE!!
    [Route("api/[controller]")]
    public class AuthenticationController : ApiControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly ITokenFactory _tokenFactory;
        private readonly IMapper _mapper;

        public AuthenticationController(UserManager<User> userManager,
            IPasswordHasher<User> passwordHasher,
            ITokenFactory tokenFactory, 
            IMapper mapper)
        {
            _userManager = userManager;
            _passwordHasher = passwordHasher;
            _tokenFactory = tokenFactory;
            _mapper = mapper;
        }

        /// <summary>
        /// Registers a new user in the database.
        /// </summary>
        [HttpPost("register")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorModel), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] RegisterModel model)
        {
            if (ModelState.IsValid)
            {
                int totalNumberOfUsers = _userManager.Users.Count();
                var user = new User
                {
                    UserName = model.Email,
                    Email = model.Email,
                    NickName = model.NickName,
                    Rank = totalNumberOfUsers + 1
                };

                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    return Ok();
                }

                //Send the errors that Identity reported in the response
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(error.Code, error.Description);
                }
            }

            return BadModel();
        }

        /// <summary>
        /// Returns an object containing a (bearer) token that will be valid for 60 minutes.
        /// The token should be added in the Authorization header of each http request for which the user must be authenticated.
        /// The Id and NickName of the player are also included in the object.
        /// <example>Authorization bearer [token]</example>
        /// </summary>
        [HttpPost("token")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(AccessPassModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorModel), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateToken([FromBody] LoginModel model)
        {
            if (!ModelState.IsValid) return BadModel();

            var user = await _userManager.FindByNameAsync(model.Email);
            if (user == null)
            {
                return Unauthorized();
            }

            if (_passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password) != PasswordVerificationResult.Success)
            {
                return Unauthorized();
            }

            var currentClaims = await _userManager.GetClaimsAsync(user);
            var accessPass = new AccessPassModel
            {
                Token = _tokenFactory.CreateToken(user, currentClaims),
                User = _mapper.Map<UserModel>(user)
            };
            return Ok(accessPass);
        }

        private BadRequestObjectResult BadModel()
        {
            foreach (ModelStateEntry entry in ModelState.Values)
            {
                foreach (ModelError error in entry.Errors)
                {
                    return BadRequest(new ErrorModel(error.ErrorMessage));
                }
            }
            throw new InvalidOperationException("Invalid operation. Bad request returned when the input is valid.");
        }
    }
}
