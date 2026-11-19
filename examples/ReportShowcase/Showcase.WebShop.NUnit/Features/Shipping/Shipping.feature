@shipping
Feature: Shipping
  Shipping is a zone base price plus a price per kilogram. Orders of 100 EUR or more ship free.

  Scenario Outline: Shipping to <zone>
    Then shipping to zone <zone> for <kg> kg costs <cost> EUR

    Examples:
      | zone  | kg   | cost  |
      | LOCAL | 0.5  | 3.15  |
      | EU    | 1.0  | 8.10  |
      | UK    | 2.5  | 14.40 |
      | US    | 0.25 | 15.50 |
      | ASIA  | 3.0  | 27.20 |

  Rule: Free shipping from 100 EUR

    Scenario: An order above 100 EUR ships free
      Given the customer ships to FR
      And the cart contains 3 x GFT-030
      Then shipping is free

    @severity:major @bug:421
    Scenario: An order of exactly 100 EUR ships free
      Given the customer ships to FR
      And the cart contains 2 x GFT-031
      Then shipping is free

    Scenario: An order below 100 EUR pays for shipping
      Given the customer ships to FR
      And the cart contains 1 x CUP-011
      Then shipping costs 8.22 EUR

  Rule: Live quotes from the carrier

    # The carrier's API is a remote service. It sometimes times out on the first call (the retry absorbs that)
    # and in build 3 it was down altogether - see the history of this scenario.
    @retry:3 @delay:200ms
    Scenario: The carrier's live quote matches the price list
      Given the customer ships to GB
      And the cart contains 1 x KIT-040
      When the carrier is asked for a live quote
      Then the live quote is 14.94 EUR

    Scenario: The carrier quote is retried on its own step only
      Given the customer ships to US
      And the cart contains 2 x CUP-010
      #retry:3 #on:TimeoutException
      When the carrier is asked for a live quote
      Then the live quote is 16.82 EUR
