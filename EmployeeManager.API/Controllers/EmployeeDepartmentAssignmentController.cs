using EmployeeManager.Application.Dtos;
using EmployeeManager.Application.Repositories;
using EmployeeManager.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeManager.API.Controllers
{
    [Route("api/assignment")]
    [ApiController]
    public class EmployeeDepartmentAssignmentController : ControllerBase
    {
        private readonly ILogger<EmployeeDepartmentAssignmentController> _logger;
        private readonly IEmployeeDepartmentAssignmentRepository _assignmentRepository;
        private readonly IEmployeeRepository _employeeRepository;

        public EmployeeDepartmentAssignmentController(
            ILogger<EmployeeDepartmentAssignmentController> logger,
            IEmployeeDepartmentAssignmentRepository assignmentRepository,
            IEmployeeRepository employeeRepository)
        {
            _logger = logger;
            _assignmentRepository = assignmentRepository;
            _employeeRepository = employeeRepository;
        }

        [HttpGet]
        [Route("{id:int}")]
        [ProducesResponseType(typeof(AssignmentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> GetAssignmentById(int id, CancellationToken cancellationToken)
        {
            var assignment = await _assignmentRepository.GetAssignmentById(id, cancellationToken);
            if (assignment is null) return NotFound();

            return Ok(ToResponse(assignment));
        }

        [HttpPost]
        [ProducesResponseType(typeof(AssignmentResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> CreateAssignment(
            [FromBody] CreateAssignmentRequest request,
            CancellationToken cancellationToken)
        {
            // Step 3: do EmployeeId and DepartmentId resolve?
            var employee = await _employeeRepository.GetEmployeeById(request.EmployeeId, cancellationToken);
            var departmentExists = await _employeeRepository.DepartmentExists(request.DepartmentId, cancellationToken);

            if (employee is null || !departmentExists)
            {
                ModelState.AddModelError(nameof(request.EmployeeId), "EmployeeId or DepartmentId does not exist.");
                return BadRequest(new ValidationProblemDetails(ModelState) { Status = StatusCodes.Status400BadRequest });
            }

            // Step 4: BR-02 - date not more than 31 days ahead
            if (request.AssignmentDate.Date > DateTime.UtcNow.Date.AddDays(31))
            {
                ModelState.AddModelError(nameof(request.AssignmentDate), "AssignmentDate cannot be more than 31 days in the future.");
                return BadRequest(new ValidationProblemDetails(ModelState) { Status = StatusCodes.Status400BadRequest });
            }

            // Step 5: BR-05 - not the employee's own permanent department
            if (request.DepartmentId == employee.DepartmentId)
            {
                ModelState.AddModelError(nameof(request.DepartmentId), "Cannot assign an employee to their own permanent department.");
                return BadRequest(new ValidationProblemDetails(ModelState) { Status = StatusCodes.Status400BadRequest });
            }

            var created = await _assignmentRepository.CreateAssignment(
                new EmployeeDepartmentAssignment
                {
                    EmployeeId = request.EmployeeId,
                    DepartmentId = request.DepartmentId,
                    AssignmentDate = request.AssignmentDate,
                    Status = AssignmentStatus.Scheduled // BR-03
                },
                cancellationToken);

            _logger.LogInformation("Created assignment with id {id}", created.AssignmentId);

            return CreatedAtAction(nameof(GetAssignmentById), new { id = created.AssignmentId }, ToResponse(created));
        }

        [HttpPut]
        [Route("{id:int}")]
        [ProducesResponseType(typeof(AssignmentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult> UpdateAssignment(
            int id,
            [FromBody] UpdateAssignmentRequest request,
            CancellationToken cancellationToken)
        {
            // Precedence step 2: does the resource exist? Outranks everything below.
            var existing = await _assignmentRepository.GetAssignmentById(id, cancellationToken);
            if (existing is null) return NotFound();

            // BR-02, only re-checked if the date is actually changed
            if (request.AssignmentDate.Date != existing.AssignmentDate.Date &&
                request.AssignmentDate.Date > DateTime.UtcNow.Date.AddDays(31))
            {
                ModelState.AddModelError(nameof(request.AssignmentDate), "AssignmentDate cannot be more than 31 days in the future.");
                return BadRequest(new ValidationProblemDetails(ModelState) { Status = StatusCodes.Status400BadRequest });
            }

            // BR-04, must run before BR-01
            if (!IsValidTransition(existing.Status, request.Status))
            {
                ModelState.AddModelError(nameof(request.Status), $"Cannot transition from {existing.Status} to {request.Status}.");
                return BadRequest(new ValidationProblemDetails(ModelState) { Status = StatusCodes.Status400BadRequest });
            }

            // BR-01, evaluated last
            if (request.Status == AssignmentStatus.Active)
            {
                var conflicting = await _assignmentRepository.HasConflictingActiveAssignment(existing.EmployeeId, existing.AssignmentId, cancellationToken);
                if (conflicting)
                {
                    return Conflict(new ProblemDetails
                    {
                        Title = "Employee already has an active assignment.",
                        Status = StatusCodes.Status409Conflict
                    });
                }
            }

            existing.AssignmentDate = request.AssignmentDate;
            existing.Status = request.Status;

            var updated = await _assignmentRepository.UpdateAssignment(existing, cancellationToken);
            if (updated is null) return NotFound(); // deleted between the two calls

            _logger.LogInformation("Updated assignment with id {id}", id);

            return Ok(ToResponse(updated));
        }

        [HttpDelete]
        [Route("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> DeleteAssignmentById(int id, CancellationToken cancellationToken)
        {
            var isDeleted = await _assignmentRepository.DeleteAssignmentIfExist(id, cancellationToken);
            if (!isDeleted) return NotFound();

            return NoContent();
        }

        private static bool IsValidTransition(AssignmentStatus current, AssignmentStatus next)
        {
            if (current == next) return true;

            return (current, next) switch
            {
                (AssignmentStatus.Scheduled, AssignmentStatus.Active) => true,
                (AssignmentStatus.Scheduled, AssignmentStatus.Cancelled) => true,
                (AssignmentStatus.Active, AssignmentStatus.Completed) => true,
                (AssignmentStatus.Active, AssignmentStatus.Cancelled) => true,
                _ => false
            };
        }

        private static AssignmentResponse ToResponse(EmployeeDepartmentAssignment a) =>
            new(a.AssignmentId, a.EmployeeId, a.DepartmentId, a.AssignmentDate, a.Status);
    }
}