Feature: Multiple hashtags per step
Scenario: Step with many hashtags
    #tag1
    #tag2
    #tag3 
    Given step validates hashtag count is 3

Scenario: Step with ignore hashtag
    #tag1
    #tag2
    #tag3 #ignore
    Given step validates hashtag count is 3

@/"\n
Scenario: Multiple steps with ignore hashtag
    #tag1
    #tag2
    #tag3 #ignore #@/"\n
    Given step validates hashtag count is 3
    #retry:3
    #timeout:5000
    And step validates hashtag "retry" has value "3"
    #ignore
    And step validates hashtag "retry" has value "3"
    #retry:3
    #timeout:5000
    And step validates hashtag "timeout" has value "5000"
    #ignore
    And step validates hashtag "timeout" has value "5000"
    #tag1
    #tag2
    #tag3 
    And step validates hashtag count is 3

Scenario: Hashtags with values
    #retry:3
    #timeout:5000
    Given step validates hashtag "retry" has value "3"
    #retry:3
    #timeout:5000
    And step validates hashtag "timeout" has value "5000"

