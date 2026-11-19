@pricing
Feature: VAT and gift wrapping

  Scenario Outline: VAT in <country>
    Then the VAT on <net> EUR in <country> is <vat> EUR

    Examples:
      | country | net    | vat   |
      | DE      | 100.00 | 19.00 |
      | PL      | 59.99  | 13.80 |
      | HU      | 10.00  | 2.70  |
      | FI      | 19.99  | 5.10  |
      | LU      | 1.00   | 0.17  |

  # Countries outside the EU are not supported yet: the step throws instead of returning a value.
  Scenario: VAT for a country without a configured rate
    Then the VAT on 10.00 EUR in CH is 0.81 EUR

  @bug:418
  Scenario: Gift wrapping costs 3.50 EUR per order
    Given the cart contains 3 x CUP-010
    And the customer asks for gift wrapping
    Then the gift wrapping fee is 3.50 EUR
