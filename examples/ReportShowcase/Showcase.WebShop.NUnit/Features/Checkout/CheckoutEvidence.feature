@checkout @p1
Feature: Checkout evidence
  What a test can attach to the report: screenshots (inline images), image URLs, HTML (here the receipt),
  callouts at three levels and links to the work items a scenario belongs to (@pbi / @bug tags). A failing
  scenario also gets a screenshot of the state it failed in and a prefilled "Report a bug" link.

  Background:
    Given a member customer
    And the customer ships to NL

  @pbi:3010
  Scenario: The receipt shows every line, the discount and shipping
    Given the cart contains:
      | sku     | quantity |
      | TEA-001 | 2        |
      | CUP-010 | 2        |
      | ACC-020 | 1        |
    When a screenshot of the cart is attached
    And the product photo of TEA-001 is attached
    Then the subtotal is 76.00 EUR
    And the receipt is attached as a table
    And an info note says "Prices include the member discount; VAT is added at payment."
    And the deployment that was verified is linked

  @pbi:3011
  Scenario: A large order is flagged for manual review
    Given the cart contains 30 x KIT-040
    When a screenshot of the cart is attached, expanded
    Then the subtotal is 2670.00 EUR
    And a warning says "Orders above 2000 EUR are reviewed by the fraud team before shipping."
    And the receipt is attached as a table

  @pbi:3012 @bug:430
  Scenario: The receipt of a gift order includes the wrapping fee once
    Given the cart contains 4 x TEA-002
    And the customer asks for gift wrapping
    When a screenshot of the cart is attached
    Then the gift wrapping fee is 3.50 EUR
    And the receipt is attached as a table

  Scenario: An order note is stored as entered
    Given the cart contains 1 x GFT-031
    And the order note is:
      """
      Please deliver to the reception.
      Gift card for: Olena - "Happy birthday!"
      """
    Then the order note is kept verbatim
    And an error callout says "Delivery notes are not shown on the courier label yet (known limitation)."
