Feature: XFail Realistic Use Cases

# Testing unstable third-party APIs
Scenario: API call may timeout but test should continue
    Given user is authenticated
    #afail:TimeoutException
    When I call unstable external API
    Then response should be logged

# Testing known bugs
Scenario: Known bug - validation throws wrong exception
    Given I have invalid data
    #xfail:ArgumentException
    When I validate the data
    Then validation message is logged

# Testing deprecated features
Scenario: Deprecated method should throw NotSupportedException
    Given I have legacy code
    #xfail:NotSupportedException  
    When I call deprecated method
    Then deprecation is logged

# Testing error handling
Scenario: System should handle network failures gracefully
    Given network is unreliable
    #afail:IOException|TimeoutException
    When I attempt file upload
    Then fallback mechanism is triggered

# Testing race conditions
Scenario: Concurrent access may cause lock timeout
    Given multiple threads access resource
    #afail:TimeoutException
    When I acquire the lock
    Then operation completes or times out gracefully

# Testing incomplete features
Scenario: Feature under development - not yet implemented
    Given feature flag is enabled
    #xfail:NotImplementedException
    When I use new feature
    Then appropriate error is logged
