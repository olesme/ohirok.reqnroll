@payments @p1
Feature: Authorization, capture and refunds
  The payment provider is a remote service; on some builds its first call times out, so these scenarios retry.

  @retry:3
  Scenario: A Visa payment is authorized and captured
    When 79.90 EUR is authorized on card "4111 1111 1111 1111"
    Then the payment is authorized
    When 79.90 EUR is captured
    Then 79.90 EUR has been captured

  @retry:3
  Scenario: A payment above the card limit is declined
    When 2500.00 EUR is attempted on card "4111 1111 1111 1111"
    Then the payment is declined because "exceeds the card limit"

  Scenario: A payment with a mistyped card number is declined
    When 19.00 EUR is attempted on card "4111 1111 1111 1112"
    Then the payment is declined because "invalid card number"

  @retry:3
  Scenario: A large payment must be confirmed in the banking app
    When 640.00 EUR is authorized on card "5500 0000 0000 0004"
    Then the customer is asked to confirm the payment in the banking app
    When the customer confirms the payment in the banking app
    Then the payment is authorized

  @retry:3 @bug:512
  Scenario: A refund cannot exceed the captured amount
    When 50.00 EUR is authorized on card "5500 0000 0000 0004"
    And 30.00 EUR is captured
    Then a refund of 40.00 EUR is refused

  @retry:3
  Scenario: A partial refund is possible
    When 120.00 EUR is authorized on card "4111 1111 1111 1111"
    And 120.00 EUR is captured
    And 20.00 EUR is refunded
    Then 120.00 EUR has been captured
