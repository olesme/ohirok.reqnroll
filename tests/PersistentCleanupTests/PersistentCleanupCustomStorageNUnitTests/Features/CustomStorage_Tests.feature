Feature: Custom Storage Operations

Scenario: Use custom storage with UseStorage extension
    Given I have a custom in-memory storage
    When I configure manager with custom storage
    And I register cleanup for "CustomUser1"
    And I perform all cleanups
    Then cleanup should have executed for "CustomUser1"
    And custom storage should contain completed record

Scenario: UseStorage throws when storage already exists
    Given cleanup manager already has storage configured
    When I try to set custom storage with ThrowIfExists mode
    Then an InvalidOperationException should be thrown

Scenario: UseStorage with IgnoreIfExists preserves existing storage
    Given I have configured storage A
    When I try to set storage B with IgnoreIfExists mode
    Then storage A should still be active
    And cleanup operations should use storage A

Scenario: UseStorage with Replace mode swaps storage
    Given I have configured storage A
    When I set storage B with Replace mode
    Then storage B should be active
    And cleanup operations should use storage B

Scenario: PerformAllAsync extension with custom storage
    Given I have a custom in-memory storage
    And I have not configured manager storage
    When I register cleanup for "ExtUser1" normally
    And I call PerformAllAsync with custom storage
    Then cleanup should have executed for "ExtUser1"
    And custom storage should contain completed record

Scenario: PerformAllAsync ignores provided storage if one exists
    Given I have configured storage A with cleanup "ExistUser1"
    When I call PerformAllAsync with storage B
    Then "ExistUser1" should execute
    And storage A should contain the execution record
    And storage B should be empty

Scenario: Custom storage concurrent operations
    Given I have a custom in-memory storage
    When I configure manager with custom storage
    And I register 20 cleanups concurrently
    And I perform all cleanups
    Then all 20 cleanups should be in custom storage
    And all 20 should have status Completed

Scenario: Custom storage preserves across operations
    Given I have a custom in-memory storage
    When I configure manager with custom storage
    And I register cleanup for "Persist1"
    And I register cleanup for "Persist2"
    Then custom storage should show 2 pending records
    When I perform all cleanups
    Then custom storage should show 2 completed records
    And cleanup should have executed for "Persist1"
    And cleanup should have executed for "Persist2"

Scenario: Custom storage handles failures correctly
    Given I have a custom in-memory storage
    When I configure manager with custom storage
    And I register failing cleanup for "FailUser" with 2 retries
    And I perform all cleanups
    Then custom storage should contain failed record
    And record should show 2 attempts
