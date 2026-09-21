namespace EmployeeManager.Core.Models;

public class EmployeeDepartmentAssignment
{
    public int AssignmentId { get; set; }

    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;

    public DateTime AssignmentDate { get; set; }
    public AssignmentStatus Status { get; set; }
}
