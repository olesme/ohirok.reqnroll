@payments
Feature: Prices in the customer's currency

  Scenario Outline: 100 EUR in <currency>
    Then 100.00 EUR is <amount> <currency>

    Examples:
      | currency | amount  |
      | EUR      | 100.00  |
      | USD      | 108.42  |
      | GBP      | 85.71   |
      | PLN      | 431.95  |
      | JPY      | 16235   |

  # The Kuwaiti dinar has three decimals.
  @bug:520
  Scenario: Prices in Kuwaiti dinar keep three decimals
    Then 12.34 EUR is 4.124 KWD
