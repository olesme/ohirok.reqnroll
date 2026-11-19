Feature: StepHashtags Library Expected Failures

  Scenario: FAIL_Hashtags with wrong count
    #retry:3
    #timeout:5000
    Given step validates hashtag count is 3
    #retry:3
    #timeout:5000
    And step validates hashtag "timeout" has value "5000"
