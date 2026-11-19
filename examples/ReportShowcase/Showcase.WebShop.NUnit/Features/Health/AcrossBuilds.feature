@health
Feature: Behaviour across builds
  These scenarios run against services that change between builds, the way real dependencies and real
  regressions do - so the history strip and the analytics show every health state: stable, flaky, regressed,
  fixed and broken. Run the showcase script to get five builds of history.

  Scenario: Stable - the product page loads its price
    Given the cart contains 1 x TEA-001
    Then the subtotal is 12.50 EUR

  # The search index is rebuilt during builds 2 and 4, so autocomplete returns nothing then.
  Scenario: Flaky - autocomplete suggests matching products
    When the customer types "Ear" into the search box
    Then the suggestions include "Earl Grey"

  # Build 4 shipped a change that no longer checks the stock level.
  @severity:critical
  Scenario: Regressed - an order larger than the stock is refused
    When the customer reserves 20 x CUP-010
    Then 5 x CUP-010 remain in stock
    And reserving 10 x CUP-010 is refused

  # Fixed in build 3: before that the product recommended itself.
  Scenario: Fixed - recommendations never suggest the product itself
    Then the product page of "TEA-002" recommends other products only

  @bug:418
  Scenario: Broken - gift wrapping is charged once per order
    Given the cart contains 2 x TEA-001
    And the customer asks for gift wrapping
    Then the gift wrapping fee is 3.50 EUR
