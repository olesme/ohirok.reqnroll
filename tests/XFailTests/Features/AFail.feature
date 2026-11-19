Feature: AFail (Allow Fail) Scenarios

Scenario: AFail step that fails is allowed
    #afail
    Given step that throws ArgumentException
    Then step verifies exception was handled

Scenario: AFail step that passes is also allowed
    #afail
    Given step that succeeds
    Then step confirms success is ok

Scenario: AFail with specific exception type that matches
    #afail:ArgumentException
    Given step that throws ArgumentException
    Then step verifies exception was handled

Scenario: AFail with specific exception but step passes
    #afail:ArgumentException
    Given step that succeeds
    Then step confirms success is ok

Scenario: AFail with multiple exception patterns
    #afail:ArgumentException|InvalidOperationException|NullReferenceException
    Given step that throws NullReferenceException
    Then step verifies exception was handled

Scenario: AFail with negative pattern
    #afail:*Exception,!TimeoutException
    Given step that throws ArgumentException
    Then step verifies exception was handled

Scenario: Expect-fail alternative syntax works
    #expect-fail:ArgumentException
    Given step that throws ArgumentException
    Then step verifies exception was handled

Scenario: Allow-fail alternative syntax works
    #allow-fail
    Given step that throws InvalidOperationException
    Then step verifies exception was handled
