Feature: Scopes, abandoned records and atomic claims

# Several services share one in-memory storage, exactly like several test processes share one database.

Scenario: A service only performs the records of its own scope
    Given an empty shared in-memory storage
    When the service with scope "shard-1" registers a cleanup for "S1User"
    And the service with scope "shard-2" registers a cleanup for "S2User"
    And the service with scope "shard-1" performs all cleanups
    Then the cleanup for "S1User" should have run
    And the cleanup for "S2User" should not have run
    And the record for "S2User" should have status "Pending"

Scenario: The wildcard scope performs the records of every scope
    Given an empty shared in-memory storage
    When the service with scope "shard-1" registers a cleanup for "S1User"
    And the service with scope "shard-2" registers a cleanup for "S2User"
    And the service with scope "*" performs all cleanups
    Then the cleanup for "S1User" should have run
    And the cleanup for "S2User" should have run

Scenario: A service without a scope does not see scoped records
    Given an empty shared in-memory storage
    When the service with scope "shard-1" registers a cleanup for "S1User"
    And the service with scope "" performs all cleanups
    Then the cleanup for "S1User" should not have run
    And the record for "S1User" should have status "Pending"

Scenario: An abandoned Processing record is performed again, a live one is left alone
    Given an empty shared in-memory storage
    And a "Processing" record for "StaleUser" last attempted 60 minutes ago
    And a "Processing" record for "FreshUser" last attempted 1 minutes ago
    When the service with scope "" performs all cleanups
    Then the cleanup for "StaleUser" should have run
    And the cleanup for "FreshUser" should not have run
    And the result should show 1 successful and 1 skipped
    And the record for "FreshUser" should have status "Processing"

Scenario: A failed record is retried by the next run with a fresh attempt budget
    Given an empty shared in-memory storage
    When the service with scope "" registers a failing cleanup for "RetryUser" with 2 retries
    And the service with scope "" performs all cleanups
    Then the failing cleanup for "RetryUser" should have been attempted 2 times
    And the record for "RetryUser" should have status "Failed"
    When the service with scope "" performs all cleanups
    Then the failing cleanup for "RetryUser" should have been attempted 4 times
    And the record for "RetryUser" should have status "Failed"

Scenario: A record whose cleanup method no longer exists becomes an orphan
    Given an empty shared in-memory storage
    And a record for a removed cleanup method with argument "GoneUser"
    When the service with scope "" performs all cleanups
    Then the result should show 1 orphan
    And the record for "GoneUser" should have status "Orphan"

Scenario: Two services performing at the same moment run every record exactly once
    Given an empty shared in-memory storage
    When 25 cleanups are registered by the service with scope ""
    And two services with scope "" perform all cleanups at the same moment
    Then every registered cleanup should have run exactly once
    And the two results together should show 25 successful
