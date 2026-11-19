@pricing
Feature: Discounts
  Members get 10% off. Large orders get a bulk discount instead: 5% from 10 units, 10% from 50 units and 15%
  from 100 units. The two never stack - the customer gets the better one.

  Rule: Loyalty discount is for members only

    Background:
      Given the cart contains 2 x TEA-003

    @severity:minor
    Scenario: A member gets 10% off
      Given a member customer
      Then the subtotal is 68.00 EUR
      And the discount is 6.80 EUR

    @severity:critical @bug:412 @pbi:3051
    Scenario: A returning guest pays the full price
      Given a returning-guest customer
      Then the discount is 0.00 EUR

    Scenario: A guest pays the full price
      Given a guest customer
      Then the discount is 0.00 EUR

  Rule: Bulk discount by number of units

    @severity:major
    Scenario Outline: <units> units of Earl Grey
      Given a guest customer
      And the cart contains <units> x TEA-002
      Then the discount is <discount> EUR

      Examples: Below and at the first tier
        | units | discount |
        | 9     | 0.00     |
        | 10    | 4.95     |
        | 49    | 24.26    |

      Examples: Second and third tier
        | units | discount |
        | 50    | 49.50    |
        | 51    | 50.49    |
        | 100   | 148.50   |

  Rule: Discounts do not stack

    Scenario: A member ordering in bulk gets the better of both discounts
      Given a member customer
      And the cart contains 120 x TEA-002
      Then the discount is 178.20 EUR
