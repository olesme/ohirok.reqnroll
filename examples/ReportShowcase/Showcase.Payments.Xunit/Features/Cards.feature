@payments
Feature: Card validation
  The shop checks card numbers with the Luhn algorithm and detects the card brand before calling the payment
  provider.

  Scenario Outline: Luhn check of <number>
    Then card "<number>" is <validity>

    Examples: Valid numbers
      | number              | validity |
      | 4111 1111 1111 1111 | valid    |
      | 5500 0000 0000 0004 | valid    |
      | 3400 0000 0000 009  | valid    |
      | 2221 0000 0000 0009 | valid    |

    Examples: Typos
      | number              | validity |
      | 4111 1111 1111 1112 | invalid  |
      | 5500 0000 0000 0005 | invalid  |
      | 1234                | invalid  |

  @severity:critical @bug:507
  Scenario Outline: Brand of <number>
    Then card "<number>" is recognised as <brand>

    Examples:
      | number              | brand      |
      | 4111 1111 1111 1111 | Visa       |
      | 5500 0000 0000 0004 | Mastercard |
      | 3700 0000 0000 002  | Amex       |
      | 2221 0000 0000 0009 | Mastercard |
      | 2720 9900 0000 0001 | Mastercard |
