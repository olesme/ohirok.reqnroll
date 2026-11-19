Feature: Orphan Detection

Scenario: Detect orphan cleanup when method is removed
    Given I have a test resource "OrphanUser"
    When I register cleanup for "OrphanUser"
    And I simulate method removal for "OrphanUser"
    And I perform all cleanups
    Then cleanup status should be "Orphan"

Scenario: Normal cleanup when method exists
    Given I have a test resource "ValidUser"
    When I register cleanup for "ValidUser"
    And I perform all cleanups
    Then cleanup should have executed for "ValidUser"
    And cleanup status should be "Completed"
