@giftcards
Feature: Gift cards
  The edge case of a report: one project with a single feature.

  Scenario: A gift card is a product like any other
    Given the cart contains 1 x GFT-031
    Then the subtotal is 50.00 EUR

  Scenario: Gift cards weigh nothing
    Given the cart contains 3 x GFT-031
    Then the order weighs 0.00 kg

  Scenario: A gift card order is not discounted twice
    Given a member customer
    And the cart contains 2 x GFT-031
    Then the discount is 10.00 EUR
