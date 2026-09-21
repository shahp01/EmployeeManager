using System.ComponentModel.DataAnnotations;
using EmployeeManager.Core.Models;

namespace EmployeeManager.Application.Dtos;

/// <summary>
/// What the API returns for an assignment.
/// </summary>
public record AssignmentResponse(int AssignmentId, int EmployeeId, int DepartmentId, DateTime AssignmentDate, AssignmentStatus Status);

/// <summary>
/// Request body for POST /api/assignment. Status is deliberately absent
/// BR-03 says a client-supplied status is ignored, not validated or rejected.
/// </summary>
public class CreateAssignmentRequest
{
    [Required]
    public int EmployeeId { get; set; }

    [Required]
    public int DepartmentId { get; set; }

    [Required]
    public DateTime AssignmentDate { get; set; }
}

/// <summary>
/// Request body for PUT /api/assignment/{id}. EmployeeId and DepartmentId
/// are immutable once created, so they don't appear here only what
/// the brief says PUT is allowed to change.
/// </summary>
public class UpdateAssignmentRequest
{
    [Required]
    public DateTime AssignmentDate { get; set; }

    [Required]
    public AssignmentStatus Status { get; set; }
}