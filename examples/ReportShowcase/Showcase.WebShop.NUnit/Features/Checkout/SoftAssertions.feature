@checkout
Feature: Soft assertions on the order summary
  A #softassert line above a step records that step's failure and lets the scenario carry on, so one run
  reports every wrong value on the summary instead of only the first. The scenario still fails at the end.

  Scenario: Every value on the order summary is checked, all correct
    Given a member customer
    And the customer ships to DE
    And the cart contains 2 x TEA-003
    #softassert
    Then the subtotal is 68.00 EUR
    #softassert
    And the discount is 6.80 EUR
    #softassert
    And shipping costs 3.00 EUR

  @bug:418 @bug:421
  Scenario: Two wrong values on the order summary are both reported
    Given a guest customer
    And the customer ships to FR
    And the cart contains 2 x GFT-031
    And the customer asks for gift wrapping
    #softassert
    Then the gift wrapping fee is 3.50 EUR
    #softassert
    And shipping is free
    #softassert
    And the subtotal is 100.00 EUR

  # A failure no step owns: SoftAssert fails the scenario from the flush step it adds at the end. The scenario tag
  # expects exactly that; for a failure inside a step, the #xfail hashtag on that step is the better choice.
  @xfail:AggregateAssertionException
  Scenario: The end-of-scenario soft-assertion failure is the expected one
    Given a guest customer
    And the cart contains 1 x TEA-001
    And the customer asks for gift wrapping
    #softassert
    Then the gift wrapping fee is 0.00 EUR
    And the subtotal is 12.50 EUR
