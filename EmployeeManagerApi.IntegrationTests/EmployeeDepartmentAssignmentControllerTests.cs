using System.Net;
using System.Net.Http.Json;
using EmployeeManager.Application.Dtos;
using EmployeeManager.Core.Models;
using EmployeeManagerApi.IntegrationTests.Urls;
using FluentAssertions;

namespace EmployeeManagerApi.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class EmployeeDepartmentAssignmentControllerTests
{
    private readonly HttpClient _client;

    // These values MUST match your integration-test seed data.
    private const int EmployeeOneId = 1;
    private const int EmployeeThreeId = 3;

    // Employee 1's permanent department.
    // Change this to the actual DepartmentId from your seed data.
    private const int EmployeeOnePermanentDepartmentId = 1;

    private const int AssignmentDepartmentOne = 7;
    private const int AssignmentDepartmentTwo = 8;

    public EmployeeDepartmentAssignmentControllerTests(ApiTestFixture fixture)
    {
        _client = fixture.Client;
    }

    [Fact]
    public async Task CreateAssignment_WithValidPayload_ShouldReturnCreatedWithStatusScheduled()
    {
        // Arrange
        var assignmentDate = DateTime.UtcNow.AddDays(5);

        var request = new CreateAssignmentRequest
        {
            EmployeeId = EmployeeOneId,
            DepartmentId = AssignmentDepartmentOne,
            AssignmentDate = assignmentDate
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            ApiRoutes.Assignments.Base,
            request);

        Console.WriteLine($"STATUS: {(int)response.StatusCode}");
        Console.WriteLine($"BODY: {response}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        response.Headers.Location.Should().NotBeNull();

        var created = await response.Content.ReadFromJsonAsync<AssignmentResponse>(
            ApiTestFixture.JsonOptions);

        created.Should().NotBeNull();

        created!.EmployeeId.Should().Be(EmployeeOneId);
        created.DepartmentId.Should().Be(AssignmentDepartmentOne);
        created.Status.Should().Be(AssignmentStatus.Scheduled);
    }

    [Fact]
    public async Task CreateAssignment_WithDateOver31DaysAhead_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new CreateAssignmentRequest
        {
            EmployeeId = EmployeeOneId,
            DepartmentId = AssignmentDepartmentOne,
            AssignmentDate = DateTime.UtcNow.AddDays(40)
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            ApiRoutes.Assignments.Base,
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateAssignment_ToEmployeesOwnPermanentDepartment_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new CreateAssignmentRequest
        {
            EmployeeId = EmployeeOneId,
            DepartmentId = EmployeeOnePermanentDepartmentId,
            AssignmentDate = DateTime.UtcNow.AddDays(5)
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            ApiRoutes.Assignments.Base,
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateAssignment_TransitionFromCompletedToActive_ShouldReturnBadRequestNotConflict()
    {
        // Arrange
        var assignmentDate = DateTime.UtcNow.AddDays(1);

        var createRequest = new CreateAssignmentRequest
        {
            EmployeeId = EmployeeOneId,
            DepartmentId = AssignmentDepartmentOne,
            AssignmentDate = assignmentDate
        };

        var createResponse = await _client.PostAsJsonAsync(
            ApiRoutes.Assignments.Base,
            createRequest);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<AssignmentResponse>(
            ApiTestFixture.JsonOptions);

        created.Should().NotBeNull();

        // Move Scheduled -> Active
        var activeResponse = await _client.PutAsJsonAsync(
            ApiRoutes.Assignments.ById(created!.AssignmentId),
            new UpdateAssignmentRequest
            {
                AssignmentDate = assignmentDate,
                Status = AssignmentStatus.Active
            });

        activeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Move Active -> Completed
        var completedResponse = await _client.PutAsJsonAsync(
            ApiRoutes.Assignments.ById(created.AssignmentId),
            new UpdateAssignmentRequest
            {
                AssignmentDate = assignmentDate,
                Status = AssignmentStatus.Completed
            });

        completedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act
        // BR-04: Completed is a terminal state.
        // Attempting Completed -> Active must return 400,
        // not 409 from the active-assignment conflict rule.
        var response = await _client.PutAsJsonAsync(
            ApiRoutes.Assignments.ById(created.AssignmentId),
            new UpdateAssignmentRequest
            {
                AssignmentDate = assignmentDate,
                Status = AssignmentStatus.Active
            });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateAssignment_SecondActiveForSameEmployee_ShouldReturnConflict()
    {
        // Arrange
        var firstAssignmentDate = DateTime.UtcNow.AddDays(1);
        var secondAssignmentDate = DateTime.UtcNow.AddDays(2);

        // First assignment
        var firstResponse = await _client.PostAsJsonAsync(
            ApiRoutes.Assignments.Base,
            new CreateAssignmentRequest
            {
                EmployeeId = EmployeeThreeId,
                DepartmentId = AssignmentDepartmentOne,
                AssignmentDate = firstAssignmentDate
            });

        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var firstCreated = await firstResponse.Content.ReadFromJsonAsync<AssignmentResponse>(
            ApiTestFixture.JsonOptions);

        firstCreated.Should().NotBeNull();

        // Second assignment
        var secondResponse = await _client.PostAsJsonAsync(
            ApiRoutes.Assignments.Base,
            new CreateAssignmentRequest
            {
                EmployeeId = EmployeeThreeId,
                DepartmentId = AssignmentDepartmentTwo,
                AssignmentDate = secondAssignmentDate
            });

        secondResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var secondCreated = await secondResponse.Content.ReadFromJsonAsync<AssignmentResponse>(
            ApiTestFixture.JsonOptions);

        secondCreated.Should().NotBeNull();

        // Activate first assignment
        var activateFirstResponse = await _client.PutAsJsonAsync(
            ApiRoutes.Assignments.ById(firstCreated!.AssignmentId),
            new UpdateAssignmentRequest
            {
                AssignmentDate = firstAssignmentDate,
                Status = AssignmentStatus.Active
            });

        activateFirstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act
        // BR-01: Employee already has an Active assignment.
        var response = await _client.PutAsJsonAsync(
            ApiRoutes.Assignments.ById(secondCreated!.AssignmentId),
            new UpdateAssignmentRequest
            {
                AssignmentDate = secondAssignmentDate,
                Status = AssignmentStatus.Active
            });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetAssignmentById_WhenAssignmentDoesNotExist_ShouldReturnNotFound()
    {
        // Arrange
        const int missingAssignmentId = 999_999;

        // Act
        var response = await _client.GetAsync(
            ApiRoutes.Assignments.ById(missingAssignmentId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}