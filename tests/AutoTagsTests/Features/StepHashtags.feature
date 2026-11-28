Feature: Hashtags from steps

    reqnroll.json gives every step starting with "the payment gateway" the hashtags #external and #retry:2.

Scenario: A matching step gets the hashtags
    When the payment gateway accepts the card
    Then that step had the hashtag external

Scenario: Retry uses a hashtag given by the configuration
    When the payment gateway fails 2 times
    Then that step had the hashtag retry:2

Scenario: A hashtag written above the step wins over the configured one
    # Written: 3 retries. Configured: 2. Failing 3 times passes only with the written value.
    #retry:3
    When the payment gateway fails 3 times

Scenario: A step that does not match gets no hashtags
    When I add the product to the cart
    Then that step had no hashtags
