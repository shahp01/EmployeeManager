using EmployeeManager.Core.Models;

namespace EmployeeManager.Application.Repositories;

public interface IEmployeeDepartmentAssignmentRepository
{
    Task<EmployeeDepartmentAssignment?> GetAssignmentById(int id, CancellationToken cancellationToken);
    Task<EmployeeDepartmentAssignment> CreateAssignment(EmployeeDepartmentAssignment assignment, CancellationToken cancellationToken);
    Task<EmployeeDepartmentAssignment?> UpdateAssignment(EmployeeDepartmentAssignment assignment, CancellationToken cancellationToken);
    Task<bool> DeleteAssignmentIfExist(int id, CancellationToken cancellationToken);
    Task<bool> HasConflictingActiveAssignment(int employeeId, int excludingAssignmentId, CancellationToken cancellationToken);
}