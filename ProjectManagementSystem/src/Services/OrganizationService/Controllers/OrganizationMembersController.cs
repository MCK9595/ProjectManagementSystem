using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ProjectManagementSystem.OrganizationService.Services;
using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;

namespace ProjectManagementSystem.OrganizationService.Controllers;

[ApiController]
[Route("api/organizations/{organizationId}/[controller]")]
[Authorize]
public class MembersController : ControllerBase
{
    private readonly IOrganizationMemberService _memberService;
    private readonly ILogger<MembersController> _logger;

    public MembersController(IOrganizationMemberService memberService, ILogger<MembersController> logger)
    {
        _memberService = memberService;
        _logger = logger;
    }

    /// <summary>
    /// Get organization members
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<OrganizationMemberDto>>>> GetMembers(
        Guid organizationId,
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(ApiResponse<PagedResult<OrganizationMemberDto>>.ErrorResult("Invalid token"));

            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var result = await _memberService.GetMembersAsync(organizationId, userId.Value, page, pageSize);
            
            return Ok(ApiResponse<PagedResult<OrganizationMemberDto>>.SuccessResult(result, "Members retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving members for organization {OrganizationId}", organizationId);
            return StatusCode(500, ApiResponse<PagedResult<OrganizationMemberDto>>.ErrorResult("An error occurred while retrieving members"));
        }
    }

    /// <summary>
    /// Add a member to the organization
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<OrganizationMemberDto>>> AddMember(
        Guid organizationId, 
        [FromBody] AddMemberDto addMemberDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(ApiResponse<OrganizationMemberDto>.ErrorResult("Invalid input", errors));
            }

            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(ApiResponse<OrganizationMemberDto>.ErrorResult("Invalid token"));

            var member = await _memberService.AddMemberAsync(organizationId, addMemberDto, userId.Value);
            
            if (member == null)
                return BadRequest(ApiResponse<OrganizationMemberDto>.ErrorResult("Unable to add member. Check permissions and member status."));

            return Ok(ApiResponse<OrganizationMemberDto>.SuccessResult(member, "Member added successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding member to organization {OrganizationId}", organizationId);
            return StatusCode(500, ApiResponse<OrganizationMemberDto>.ErrorResult("An error occurred while adding the member"));
        }
    }

    /// <summary>
    /// Add a member to the organization by email (find or create user)
    /// </summary>
    [HttpPost("by-email")]
    public async Task<ActionResult<ApiResponse<OrganizationMemberDto>>> AddMemberByEmail(
        Guid organizationId, 
        [FromBody] AddMemberByEmailDto addMemberDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(ApiResponse<OrganizationMemberDto>.ErrorResult("Invalid input", errors));
            }

            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(ApiResponse<OrganizationMemberDto>.ErrorResult("Invalid token"));

            var member = await _memberService.AddMemberByEmailAsync(organizationId, addMemberDto, userId.Value);
            
            if (member == null)
                return BadRequest(ApiResponse<OrganizationMemberDto>.ErrorResult("Unable to add member. Check permissions, member status, or user creation failed."));

            return Ok(ApiResponse<OrganizationMemberDto>.SuccessResult(member, "Member added successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding member by email to organization {OrganizationId}", organizationId);
            return StatusCode(500, ApiResponse<OrganizationMemberDto>.ErrorResult("An error occurred while adding the member"));
        }
    }

    /// <summary>
    /// Add an existing user to the organization by email
    /// </summary>
    [HttpPost("find-and-add")]
    public async Task<ActionResult<ApiResponse<OrganizationMemberDto>>> FindAndAddMember(
        Guid organizationId, 
        [FromBody] FindUserByEmailDto findUserDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(ApiResponse<OrganizationMemberDto>.ErrorResult("Invalid input", errors));
            }

            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(ApiResponse<OrganizationMemberDto>.ErrorResult("Invalid token"));

            var member = await _memberService.AddExistingUserByEmailAsync(organizationId, findUserDto, userId.Value);
            
            if (member == null)
                return BadRequest(ApiResponse<OrganizationMemberDto>.ErrorResult("Unable to add member. User may not exist, already be a member, or you may not have proper permissions."));

            return Ok(ApiResponse<OrganizationMemberDto>.SuccessResult(member, "Existing member added successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding and adding member to organization {OrganizationId}", organizationId);
            return StatusCode(500, ApiResponse<OrganizationMemberDto>.ErrorResult("An error occurred while adding the member"));
        }
    }

    /// <summary>
    /// Create a new user and add them to the organization
    /// </summary>
    [HttpPost("create-and-add")]
    public async Task<ActionResult<ApiResponse<OrganizationMemberDto>>> CreateAndAddMember(
        Guid organizationId, 
        [FromBody] CreateUserAndAddMemberDto createUserDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(ApiResponse<OrganizationMemberDto>.ErrorResult("Invalid input", errors));
            }

            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(ApiResponse<OrganizationMemberDto>.ErrorResult("Invalid token"));

            var member = await _memberService.CreateUserAndAddMemberAsync(organizationId, createUserDto, userId.Value);
            
            if (member == null)
                return BadRequest(ApiResponse<OrganizationMemberDto>.ErrorResult("Unable to create and add member. Check permissions, password confirmation, or user data validity."));

            return Ok(ApiResponse<OrganizationMemberDto>.SuccessResult(member, "User created and added as member successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating and adding member to organization {OrganizationId}", organizationId);
            return StatusCode(500, ApiResponse<OrganizationMemberDto>.ErrorResult("An error occurred while creating and adding the member"));
        }
    }

    /// <summary>
    /// Remove a member from the organization
    /// </summary>
    [HttpDelete("{userId}")]
    public async Task<ActionResult<ApiResponse<object>>> RemoveMember(Guid organizationId, int userId)
    {
        try
        {
            var requestingUserId = GetCurrentUserId();
            if (requestingUserId == null)
                return Unauthorized(ApiResponse<object>.ErrorResult("Invalid token"));

            var success = await _memberService.RemoveMemberAsync(organizationId, userId, requestingUserId.Value);
            
            if (!success)
                return BadRequest(ApiResponse<object>.ErrorResult("Unable to remove member. Check permissions."));

            return Ok(ApiResponse<object>.SuccessResult(new object(), "Member removed successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing member {UserId} from organization {OrganizationId}", userId, organizationId);
            return StatusCode(500, ApiResponse<object>.ErrorResult("An error occurred while removing the member"));
        }
    }

    /// <summary>
    /// Update a member's role
    /// </summary>
    [HttpPut("{userId}/role")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateMemberRole(
        Guid organizationId, 
        int userId, 
        [FromBody] UpdateMemberRoleRequest request)
    {
        try
        {
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(request.Role))
            {
                return BadRequest(ApiResponse<object>.ErrorResult("Invalid role specified"));
            }

            var requestingUserId = GetCurrentUserId();
            if (requestingUserId == null)
                return Unauthorized(ApiResponse<object>.ErrorResult("Invalid token"));

            var success = await _memberService.UpdateMemberRoleAsync(organizationId, userId, request.Role, requestingUserId.Value);
            
            if (!success)
                return BadRequest(ApiResponse<object>.ErrorResult("Unable to update member role. Check permissions."));

            return Ok(ApiResponse<object>.SuccessResult(new object(), "Member role updated successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating role for member {UserId} in organization {OrganizationId}", userId, organizationId);
            return StatusCode(500, ApiResponse<object>.ErrorResult("An error occurred while updating the member role"));
        }
    }

    /// <summary>
    /// Transfer organization ownership to another member
    /// </summary>
    [HttpPost("transfer-ownership")]
    public async Task<ActionResult<ApiResponse<object>>> TransferOwnership(
        Guid organizationId,
        [FromBody] TransferOwnershipRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(ApiResponse<object>.ErrorResult("Invalid input", errors));
            }

            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
                return Unauthorized(ApiResponse<object>.ErrorResult("Invalid token"));

            var success = await _memberService.TransferOwnershipAsync(organizationId, request.NewOwnerId, currentUserId.Value);
            
            if (!success)
                return BadRequest(ApiResponse<object>.ErrorResult("Unable to transfer ownership. Check permissions and ensure the new owner is a member."));

            return Ok(ApiResponse<object>.SuccessResult(new object(), "Ownership transferred successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transferring ownership to user {NewOwnerId} in organization {OrganizationId}", request.NewOwnerId, organizationId);
            return StatusCode(500, ApiResponse<object>.ErrorResult("An error occurred while transferring ownership"));
        }
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

public class UpdateMemberRoleRequest
{
    [Required]
    public required string Role { get; set; }
}

public class TransferOwnershipRequest
{
    [Required]
    public required int NewOwnerId { get; set; }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false)]
public class RequiredAttribute : System.ComponentModel.DataAnnotations.RequiredAttribute { }