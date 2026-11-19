Feature: XFail Edge Cases and Integration

Scenario: Multiple steps with different xfail/afail hashtags
    #xfail:ArgumentException
    Given step that throws ArgumentException
    #afail
    And step that succeeds
    #xfail
    And step that throws NullReferenceException
    Then step confirms all handled correctly

Scenario: Step without xfail/afail hashtag behaves normally on success
    Given step that succeeds
    Then step confirms normal behavior

Scenario: XFail with full exception name
    #xfail:System.ArgumentNullException
    Given step that throws ArgumentNullException
    Then step verifies exception was handled

Scenario: XFail with pattern matching partial name
    #xfail:Argument*
    Given step that throws ArgumentException
    Then step verifies exception was handled

Scenario: Case-insensitive exception matching
    #xfail:argumentexception
    Given step that throws ArgumentException
    Then step verifies exception was handled

Scenario: ExpectFail one-word syntax
    #expectfail
    Given step that throws ArgumentException
    Then step verifies exception was handled

Scenario: AllowFail one-word syntax
    #allowfail
    Given step that succeeds
    Then step confirms success is ok
