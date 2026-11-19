Feature: Grouped XFail and AFail Patterns

  Rule: AFail accepts both thrown exceptions and passing steps

    Scenario Outline: AFail handles various step outcomes <id>
      #afail
      Given <step>
      Then <outcome>

      Examples: Steps that throw
        | id  | step                                        | outcome                             |
        | arg | step that throws ArgumentException          | step verifies exception was handled |
        | inv | step that throws InvalidOperationException  | step verifies exception was handled |
        | nul | step that throws NullReferenceException     | step verifies exception was handled |

      Examples: Steps that succeed
        | id   | step               | outcome                     |
        | pass | step that succeeds | step confirms success is ok |

  Rule: XFail catches the expected exception type

    Scenario Outline: XFail handles expected exception across multiple runs <id>
      #xfail:ArgumentException
      Given step that throws ArgumentException
      Then step verifies exception was handled

      Examples: First group
        | id |
        | 1  |
        | 2  |

      Examples: Second group
        | id |
        | 3  |
        | 4  |

  Rule: Multiple hashtag combinations on same step

    Scenario: Multiple hashtags coexist with xfail
      #timeout:5000
      #retry:3
      #xfail
      Given step throws exception and saves hashtags to context
      Then saved hashtags contain:
        | hashtag      |
        | timeout:5000 |
        | retry:3      |
        | xfail        |
