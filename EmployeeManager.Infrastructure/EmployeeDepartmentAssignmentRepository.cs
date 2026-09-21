using EmployeeManager.Application.Repositories;
using EmployeeManager.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManager.Infrastructure;

public class EmployeeDepartmentAssignmentRepository : IEmployeeDepartmentAssignmentRepository
{
    protected readonly AppDbContext _context;

    public EmployeeDepartmentAssignmentRepository(AppDbContext context)
    {
        _context = context;
    }

    // AsNoTracking is correct here - this is a pure read, no mutation follows.
    public async Task<EmployeeDepartmentAssignment?> GetAssignmentById(int id, CancellationToken cancellationToken = default)
    {
        var requestedAssignment = await _context.EmployeeDepartmentAssignments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AssignmentId == id, cancellationToken);

        return requestedAssignment;
    }

    public async Task<EmployeeDepartmentAssignment> CreateAssignment(EmployeeDepartmentAssignment assignment, CancellationToken cancellationToken = default)
    {
        _context.EmployeeDepartmentAssignments.Add(assignment);
        await _context.SaveChangesAsync(cancellationToken);

        // SaveChangesAsync populates AssignmentId on the tracked instance.
        return assignment;
    }

    public async Task<EmployeeDepartmentAssignment?> UpdateAssignment(EmployeeDepartmentAssignment assignment, CancellationToken cancellationToken = default)
    {
        // No AsNoTracking here - the entity must be tracked for EF to detect the changes.
        var existing = await _context.EmployeeDepartmentAssignments
            .FirstOrDefaultAsync(a => a.AssignmentId == assignment.AssignmentId, cancellationToken);

        if (existing is null) return null;

        existing.AssignmentDate = assignment.AssignmentDate;
        existing.Status = assignment.Status;

        await _context.SaveChangesAsync(cancellationToken);

        return existing;
    }

    public async Task<bool> DeleteAssignmentIfExist(int id, CancellationToken cancellationToken = default)
    {
        // Tracked query: Remove() needs the entity attached to the change tracker.
        var requestedAssignment = await _context.EmployeeDepartmentAssignments
            .FirstOrDefaultAsync(a => a.AssignmentId == id, cancellationToken);

        if (requestedAssignment is null) return false;

        _context.EmployeeDepartmentAssignments.Remove(requestedAssignment);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> HasConflictingActiveAssignment(int employeeId, int excludingAssignmentId, CancellationToken cancellationToken = default)
    {
        return await _context.EmployeeDepartmentAssignments
            .AsNoTracking()
            .AnyAsync(a => a.EmployeeId == employeeId
                        && a.AssignmentId != excludingAssignmentId
                        && a.Status == AssignmentStatus.Active,
                cancellationToken);
    }
}