namespace TerrariaSplit;

internal static class EmbeddedDefaults
{
    public const string SettingsJson = """
{
  "PauseResumeKey": "F12",
  "ResetKey": "F6",
  "MouseClickThroughKey": "F9",
  "CreateWorldKey": "F7",
  "PracticeWorldKey": "F8",
  "ShowMouseClickThroughIndicator": true,
  "Language": "\u4E2D\u6587",
  "AlwaysOnTop": true,
  "PracticeMode": false,
  "SplitRoute": [
    {
      "Id": "split:item-857",
      "Enabled": true,
      "DisplayName": "\u673A\u52A8\u9970\u54C1",
      "Condition": {
        "Kind": "AtLeast",
        "Children": [
          {
            "Kind": "Fact",
            "Children": [],
            "FactKey": "item:857:ever-owned-count",
            "Comparison": "AtLeast",
            "Value": 1
          },
          {
            "Kind": "Fact",
            "Children": [],
            "FactKey": "item:53:ever-owned-count",
            "Comparison": "AtLeast",
            "Value": 1
          },
          {
            "Kind": "Fact",
            "Children": [],
            "FactKey": "item:934:ever-owned-count",
            "Comparison": "AtLeast",
            "Value": 1
          }
        ],
        "FactKey": "",
        "Comparison": "IsTrue",
        "Value": 1
      },
      "IconTargetIds": [
        "item:857",
        "item:53",
        "item:934"
      ],
      "IconOverride": {
        "Source": "All",
        "TargetId": "",
        "FilePath": ""
      },
      "IsAttached": true,
      "UseAdvancedConditionEditor": false
    },
    {
      "Id": "split:biome-jungle",
      "Enabled": true,
      "DisplayName": "\u4E1B\u6797",
      "Condition": {
        "Kind": "AtLeast",
        "Children": [
          {
            "Kind": "Fact",
            "Children": [],
            "FactKey": "biome:jungle:active",
            "Comparison": "IsTrue",
            "Value": 1
          }
        ],
        "FactKey": "",
        "Comparison": "IsTrue",
        "Value": 1
      },
      "IconTargetIds": [
        "biome:jungle"
      ],
      "IconOverride": {
        "Source": "All",
        "TargetId": "",
        "FilePath": ""
      },
      "IsAttached": true,
      "UseAdvancedConditionEditor": false
    },
    {
      "Id": "split:item-167",
      "Enabled": true,
      "DisplayName": "\u96F7\u7BA1",
      "Condition": {
        "Kind": "AtLeast",
        "Children": [
          {
            "Kind": "Fact",
            "Children": [],
            "FactKey": "item:167:ever-owned-count",
            "Comparison": "AtLeast",
            "Value": 50
          }
        ],
        "FactKey": "",
        "Comparison": "IsTrue",
        "Value": 1
      },
      "IconTargetIds": [
        "item:167"
      ],
      "IconOverride": {
        "Source": "All",
        "TargetId": "",
        "FilePath": ""
      },
      "IsAttached": true,
      "UseAdvancedConditionEditor": false
    },
    {
      "Id": "split:biome-underworld",
      "Enabled": true,
      "DisplayName": "\u5730\u72F1",
      "Condition": {
        "Kind": "AtLeast",
        "Children": [
          {
            "Kind": "Fact",
            "Children": [],
            "FactKey": "biome:underworld:active",
            "Comparison": "IsTrue",
            "Value": 1
          }
        ],
        "FactKey": "",
        "Comparison": "IsTrue",
        "Value": 1
      },
      "IconTargetIds": [
        "biome:underworld"
      ],
      "IconOverride": {
        "Source": "All",
        "TargetId": "",
        "FilePath": ""
      },
      "IsAttached": true,
      "UseAdvancedConditionEditor": false
    },
    {
      "Id": "split:boss-skeletron",
      "Enabled": true,
      "DisplayName": "\u9AB7\u9AC5\u738B",
      "Condition": {
        "Kind": "AtLeast",
        "Children": [
          {
            "Kind": "Fact",
            "Children": [],
            "FactKey": "boss:skeletron:defeated",
            "Comparison": "IsTrue",
            "Value": 1
          }
        ],
        "FactKey": "",
        "Comparison": "IsTrue",
        "Value": 1
      },
      "IconTargetIds": [
        "boss:skeletron"
      ],
      "IconOverride": {
        "Source": "All",
        "TargetId": "",
        "FilePath": ""
      },
      "IsAttached": false,
      "UseAdvancedConditionEditor": false
    },
    {
      "Id": "split:boss-wall-of-flesh",
      "Enabled": true,
      "DisplayName": "\u8840\u8089\u5899",
      "Condition": {
        "Kind": "AtLeast",
        "Children": [
          {
            "Kind": "Fact",
            "Children": [],
            "FactKey": "boss:wall-of-flesh:defeated",
            "Comparison": "IsTrue",
            "Value": 1
          }
        ],
        "FactKey": "",
        "Comparison": "IsTrue",
        "Value": 1
      },
      "IconTargetIds": [
        "boss:wall-of-flesh"
      ],
      "IconOverride": {
        "Source": "All",
        "TargetId": "",
        "FilePath": ""
      },
      "IsAttached": false,
      "UseAdvancedConditionEditor": false
    },
    {
      "Id": "split:item-525",
      "Enabled": true,
      "DisplayName": "\u4E8C\u7EA7\u7827",
      "Condition": {
        "Kind": "AtLeast",
        "Children": [
          {
            "Kind": "Fact",
            "Children": [],
            "FactKey": "item:525:ever-owned-count",
            "Comparison": "AtLeast",
            "Value": 1
          },
          {
            "Kind": "Fact",
            "Children": [],
            "FactKey": "item:1220:ever-owned-count",
            "Comparison": "AtLeast",
            "Value": 1
          },
          {
            "Kind": "Fact",
            "Children": [],
            "FactKey": "item:1105:ever-owned-count",
            "Comparison": "AtLeast",
            "Value": 40
          },
          {
            "Kind": "Fact",
            "Children": [],
            "FactKey": "item:365:ever-owned-count",
            "Comparison": "AtLeast",
            "Value": 40
          },
          {
            "Kind": "Fact",
            "Children": [],
            "FactKey": "item:382:ever-owned-count",
            "Comparison": "AtLeast",
            "Value": 10
          },
          {
            "Kind": "Fact",
            "Children": [],
            "FactKey": "item:1191:ever-owned-count",
            "Comparison": "AtLeast",
            "Value": 12
          }
        ],
        "FactKey": "",
        "Comparison": "IsTrue",
        "Value": 1
      },
      "IconTargetIds": [
        "item:525",
        "item:1220",
        "item:1105",
        "item:365",
        "item:382",
        "item:1191"
      ],
      "IconOverride": {
        "Source": "Target",
        "TargetId": "item:525",
        "FilePath": ""
      },
      "IsAttached": true,
      "UseAdvancedConditionEditor": false
    },
    {
      "Id": "split:item-520",
      "Enabled": true,
      "DisplayName": "\u53EC\u5524\u7269\u51C6\u5907",
      "Condition": {
        "Kind": "AtLeast",
        "Children": [
          {
            "Kind": "All",
            "Children": [
              {
                "Kind": "Fact",
                "Children": [],
                "FactKey": "item:43:ever-owned-count",
                "Comparison": "AtLeast",
                "Value": 1
              },
              {
                "Kind": "Fact",
                "Children": [],
                "FactKey": "item:520:ever-owned-count",
                "Comparison": "AtLeast",
                "Value": 9
              },
              {
                "Kind": "Fact",
                "Children": [],
                "FactKey": "item:521:ever-owned-count",
                "Comparison": "AtLeast",
                "Value": 9
              },
              {
                "Kind": "Fact",
                "Children": [],
                "FactKey": "item:1330:ever-owned-count",
                "Comparison": "AtLeast",
                "Value": 6
              }
            ],
            "FactKey": "",
            "Comparison": "IsTrue",
            "Value": 1
          },
          {
            "Kind": "All",
            "Children": [
              {
                "Kind": "Fact",
                "Children": [],
                "FactKey": "item:544:ever-owned-count",
                "Comparison": "AtLeast",
                "Value": 1
              },
              {
                "Kind": "Fact",
                "Children": [],
                "FactKey": "item:520:ever-owned-count",
                "Comparison": "AtLeast",
                "Value": 3
              },
              {
                "Kind": "Fact",
                "Children": [],
                "FactKey": "item:521:ever-owned-count",
                "Comparison": "AtLeast",
                "Value": 9
              },
              {
                "Kind": "Fact",
                "Children": [],
                "FactKey": "item:1330:ever-owned-count",
                "Comparison": "AtLeast",
                "Value": 6
              }
            ],
            "FactKey": "",
            "Comparison": "IsTrue",
            "Value": 1
          },
          {
            "Kind": "All",
            "Children": [
              {
                "Kind": "Fact",
                "Children": [],
                "FactKey": "item:556:ever-owned-count",
                "Comparison": "AtLeast",
                "Value": 1
              },
              {
                "Kind": "Fact",
                "Children": [],
                "FactKey": "item:43:ever-owned-count",
                "Comparison": "AtLeast",
                "Value": 1
              },
              {
                "Kind": "Fact",
                "Children": [],
                "FactKey": "item:520:ever-owned-count",
                "Comparison": "AtLeast",
                "Value": 9
              },
              {
                "Kind": "Fact",
                "Children": [],
                "FactKey": "item:521:ever-owned-count",
                "Comparison": "AtLeast",
                "Value": 3
              }
            ],
            "FactKey": "",
            "Comparison": "IsTrue",
            "Value": 1
          },
          {
            "Kind": "All",
            "Children": [
              {
                "Kind": "Fact",
                "Children": [],
                "FactKey": "item:557:ever-owned-count",
                "Comparison": "AtLeast",
                "Value": 1
              },
              {
                "Kind": "Fact",
                "Children": [],
                "FactKey": "item:43:ever-owned-count",
                "Comparison": "AtLeast",
                "Value": 1
              },
              {
                "Kind": "Fact",
                "Children": [],
                "FactKey": "item:520:ever-owned-count",
                "Comparison": "AtLeast",
                "Value": 6
              },
              {
                "Kind": "Fact",
                "Children": [],
                "FactKey": "item:521:ever-owned-count",
                "Comparison": "AtLeast",
                "Value": 6
              },
              {
                "Kind": "Fact",
                "Children": [],
                "FactKey": "item:1330:ever-owned-count",
                "Comparison": "AtLeast",
                "Value": 6
              }
            ],
            "FactKey": "",
            "Comparison": "IsTrue",
            "Value": 1
          },
          {
            "Kind": "All",
            "Children": [
              {
                "Kind": "Fact",
                "Children": [],
                "FactKey": "item:544:ever-owned-count",
                "Comparison": "AtLeast",
                "Value": 1
              },
              {
                "Kind": "Fact",
                "Children": [],
                "FactKey": "item:556:ever-owned-count",
                "Comparison": "AtLeast",
                "Value": 1
              },
              {
                "Kind": "Fact",
                "Children": [],
                "FactKey": "item:520:ever-owned-count",
                "Comparison": "AtLeast",
                "Value": 3
              },
              {
                "Kind": "Fact",
                "Children": [],
                "FactKey": "item:521:ever-owned-count",
                "Comparison": "AtLeast",
                "Value": 3
              }
            ],
            "FactKey": "",
            "Comparison": "IsTrue",
            "Value": 1
          },
          {
            "Kind": "All",
            "Children": [
              {
                "Kind": "Fact",
                "Children": [],
                "FactKey": "item:544:ever-owned-count",
                "Comparison": "AtLeast",
                "Value": 1
              },
              {
                "Kind": "Fact",
                "Children": [],
                "FactKey": "item:557:ever-owned-count",
                "Comparison": "AtLeast",
                "Value": 1
              },
              {
                "Kind": "Fact",
                "Children": [],
                "FactKey": "item:521:ever-owned-count",
                "Comparison": "AtLeast",
                "Value": 6
              },
              {
                "Kind": "Fact",
                "Children": [],
                "FactKey": "item:1330:ever-owned-count",
                "Comparison": "AtLeast",
                "Value": 6
              }
            ],
            "FactKey": "",
            "Comparison": "IsTrue",
            "Value": 1
          },
          {
            "Kind": "All",
            "Children": [
              {
                "Kind": "Fact",
                "Children": [],
                "FactKey": "item:556:ever-owned-count",
                "Comparison": "AtLeast",
                "Value": 1
              },
              {
                "Kind": "Fact",
                "Children": [],
                "FactKey": "item:557:ever-owned-count",
                "Comparison": "AtLeast",
                "Value": 1
              },
              {
                "Kind": "Fact",
                "Children": [],
                "FactKey": "item:43:ever-owned-count",
                "Comparison": "AtLeast",
                "Value": 1
              },
              {
                "Kind": "Fact",
                "Children": [],
                "FactKey": "item:520:ever-owned-count",
                "Comparison": "AtLeast",
                "Value": 6
              }
            ],
            "FactKey": "",
            "Comparison": "IsTrue",
            "Value": 1
          },
          {
            "Kind": "All",
            "Children": [
              {
                "Kind": "Fact",
                "Children": [],
                "FactKey": "item:544:ever-owned-count",
                "Comparison": "AtLeast",
                "Value": 1
              },
              {
                "Kind": "Fact",
                "Children": [],
                "FactKey": "item:556:ever-owned-count",
                "Comparison": "AtLeast",
                "Value": 1
              },
              {
                "Kind": "Fact",
                "Children": [],
                "FactKey": "item:557:ever-owned-count",
                "Comparison": "AtLeast",
                "Value": 1
              }
            ],
            "FactKey": "",
            "Comparison": "IsTrue",
            "Value": 1
          }
        ],
        "FactKey": "",
        "Comparison": "IsTrue",
        "Value": 1
      },
      "IconTargetIds": [
        "item:43",
        "item:520",
        "item:521",
        "item:1330",
        "item:544",
        "item:556",
        "item:557"
      ],
      "IconOverride": {
        "Source": "Target",
        "TargetId": "item:521",
        "FilePath": ""
      },
      "IsAttached": true,
      "UseAdvancedConditionEditor": true
    },
    {
      "Id": "split:biome-aether",
      "Enabled": true,
      "DisplayName": "\u4EE5\u592A",
      "Condition": {
        "Kind": "AtLeast",
        "Children": [
          {
            "Kind": "Fact",
            "Children": [],
            "FactKey": "biome:aether:active",
            "Comparison": "IsTrue",
            "Value": 1
          }
        ],
        "FactKey": "",
        "Comparison": "IsTrue",
        "Value": 1
      },
      "IconTargetIds": [
        "biome:aether"
      ],
      "IconOverride": {
        "Source": "All",
        "TargetId": "",
        "FilePath": ""
      },
      "IsAttached": true,
      "UseAdvancedConditionEditor": false
    },
    {
      "Id": "split:boss-destroyer",
      "Enabled": true,
      "DisplayName": "\u4E09\u738B",
      "Condition": {
        "Kind": "AtLeast",
        "Children": [
          {
            "Kind": "Fact",
            "Children": [],
            "FactKey": "boss:destroyer:defeated",
            "Comparison": "IsTrue",
            "Value": 1
          },
          {
            "Kind": "Fact",
            "Children": [],
            "FactKey": "boss:skeletron-prime:defeated",
            "Comparison": "IsTrue",
            "Value": 1
          },
          {
            "Kind": "Fact",
            "Children": [],
            "FactKey": "boss:twins:defeated",
            "Comparison": "IsTrue",
            "Value": 1
          }
        ],
        "FactKey": "",
        "Comparison": "IsTrue",
        "Value": 3
      },
      "IconTargetIds": [
        "boss:destroyer",
        "boss:skeletron-prime",
        "boss:twins"
      ],
      "IconOverride": {
        "Source": "All",
        "TargetId": "",
        "FilePath": ""
      },
      "IsAttached": false,
      "UseAdvancedConditionEditor": false
    },
    {
      "Id": "split:boss-plantera",
      "Enabled": true,
      "DisplayName": "\u4E16\u7EAA\u4E4B\u82B1",
      "Condition": {
        "Kind": "AtLeast",
        "Children": [
          {
            "Kind": "Fact",
            "Children": [],
            "FactKey": "boss:plantera:defeated",
            "Comparison": "IsTrue",
            "Value": 1
          }
        ],
        "FactKey": "",
        "Comparison": "IsTrue",
        "Value": 1
      },
      "IconTargetIds": [
        "boss:plantera"
      ],
      "IconOverride": {
        "Source": "All",
        "TargetId": "",
        "FilePath": ""
      },
      "IsAttached": false,
      "UseAdvancedConditionEditor": false
    },
    {
      "Id": "split:boss-golem",
      "Enabled": true,
      "DisplayName": "\u77F3\u5DE8\u4EBA",
      "Condition": {
        "Kind": "AtLeast",
        "Children": [
          {
            "Kind": "Fact",
            "Children": [],
            "FactKey": "boss:golem:defeated",
            "Comparison": "IsTrue",
            "Value": 1
          }
        ],
        "FactKey": "",
        "Comparison": "IsTrue",
        "Value": 1
      },
      "IconTargetIds": [
        "boss:golem"
      ],
      "IconOverride": {
        "Source": "All",
        "TargetId": "",
        "FilePath": ""
      },
      "IsAttached": false,
      "UseAdvancedConditionEditor": false
    },
    {
      "Id": "split:boss-lunatic-cultist",
      "Enabled": true,
      "DisplayName": "\u62DC\u6708\u6559\u90AA\u6559\u5F92",
      "Condition": {
        "Kind": "AtLeast",
        "Children": [
          {
            "Kind": "Fact",
            "Children": [],
            "FactKey": "boss:lunatic-cultist:defeated",
            "Comparison": "IsTrue",
            "Value": 1
          }
        ],
        "FactKey": "",
        "Comparison": "IsTrue",
        "Value": 1
      },
      "IconTargetIds": [
        "boss:lunatic-cultist"
      ],
      "IconOverride": {
        "Source": "All",
        "TargetId": "",
        "FilePath": ""
      },
      "IsAttached": false,
      "UseAdvancedConditionEditor": false
    },
    {
      "Id": "split:item-3459",
      "Enabled": true,
      "DisplayName": "\u661F\u5C18",
      "Condition": {
        "Kind": "AtLeast",
        "Children": [
          {
            "Kind": "Fact",
            "Children": [],
            "FactKey": "item:3459:ever-owned-count",
            "Comparison": "AtLeast",
            "Value": 1
          }
        ],
        "FactKey": "",
        "Comparison": "IsTrue",
        "Value": 1
      },
      "IconTargetIds": [
        "item:3459"
      ],
      "IconOverride": {
        "Source": "All",
        "TargetId": "",
        "FilePath": ""
      },
      "IsAttached": true,
      "UseAdvancedConditionEditor": false
    },
    {
      "Id": "split:item-3601",
      "Enabled": true,
      "DisplayName": "\u5929\u754C\u7B26",
      "Condition": {
        "Kind": "AtLeast",
        "Children": [
          {
            "Kind": "Fact",
            "Children": [],
            "FactKey": "item:3601:ever-owned-count",
            "Comparison": "AtLeast",
            "Value": 1
          }
        ],
        "FactKey": "",
        "Comparison": "IsTrue",
        "Value": 1
      },
      "IconTargetIds": [
        "item:3601"
      ],
      "IconOverride": {
        "Source": "All",
        "TargetId": "",
        "FilePath": ""
      },
      "IsAttached": true,
      "UseAdvancedConditionEditor": false
    },
    {
      "Id": "split:boss-moon-lord",
      "Enabled": true,
      "DisplayName": "\u6708\u4EAE\u9886\u4E3B",
      "Condition": {
        "Kind": "AtLeast",
        "Children": [
          {
            "Kind": "Fact",
            "Children": [],
            "FactKey": "boss:moon-lord:defeated",
            "Comparison": "IsTrue",
            "Value": 1
          }
        ],
        "FactKey": "",
        "Comparison": "IsTrue",
        "Value": 1
      },
      "IconTargetIds": [
        "boss:moon-lord"
      ],
      "IconOverride": {
        "Source": "All",
        "TargetId": "",
        "FilePath": ""
      },
      "IsAttached": false,
      "UseAdvancedConditionEditor": false
    }
  ],
  "ExpandSplitDetails": true,
  "CollapseSplitDetailsOnCompletion": true,
  "AutoHideAttachedGroups": true,
  "AttachedGroupsAffectTimerComparison": true,
  "ReferenceSplitSets": [],
  "ActiveReferenceSplitSet": "WR",
  "UsePersonalBestAsReferenceTime": false,
  "PersonalBestTimeSets": [],
  "ActivePersonalBestTimeSet": "Personal",
  "PersonalBestSegmentSets": [],
  "ActivePersonalBestSegmentSet": "Personal",
  "PersonalBestTimes": {
    "condition:split:item-857:complete": "",
    "condition:split:biome-jungle:complete": "",
    "condition:split:item-167:complete": "",
    "condition:split:biome-underworld:complete": "",
    "condition:split:boss-skeletron:boss-skeletron-defeated-istrue-1": "",
    "condition:split:boss-wall-of-flesh:boss-wall-of-flesh-defeated-istrue-1": "",
    "condition:split:item-525:complete": "",
    "condition:split:item-520:complete": "",
    "condition:split:biome-aether:complete": "",
    "condition:split:boss-destroyer:boss-destroyer-defeated-istrue-1": "",
    "condition:split:boss-destroyer:boss-skeletron-prime-defeated-istrue-1": "",
    "condition:split:boss-destroyer:boss-twins-defeated-istrue-1": "",
    "condition:split:boss-plantera:boss-plantera-defeated-istrue-1": "",
    "condition:split:boss-golem:boss-golem-defeated-istrue-1": "",
    "condition:split:boss-lunatic-cultist:boss-lunatic-cultist-defeated-istrue-1": "",
    "condition:split:item-3459:complete": "",
    "condition:split:item-3601:complete": "",
    "condition:split:boss-moon-lord:boss-moon-lord-defeated-istrue-1": ""
  },
  "PersonalBestSegmentTimes": {
    "split:boss-skeletron": "",
    "split:boss-wall-of-flesh": "",
    "split:boss-destroyer": "",
    "split:boss-plantera": "",
    "split:boss-golem": "",
    "split:boss-lunatic-cultist": "",
    "split:boss-moon-lord": ""
  },
  "AutoUpdatePersonalBestData": true,
  "AskBeforeUpdatingPersonalBestData": true,
  "ShowSplitCompletionAnimation": true,
  "SplitCompletionAnimationDurationSeconds": 6,
  "SplitCompletionOutlineThicknessPercent": 25,
  "SplitCompletionSplitComparisons": {
    "split:boss-skeletron": true,
    "split:boss-wall-of-flesh": true,
    "split:boss-destroyer": true,
    "split:boss-plantera": true,
    "split:boss-golem": true,
    "split:boss-lunatic-cultist": true,
    "split:boss-moon-lord": true
  },
  "SplitCompletionSegmentComparisons": {
    "split:boss-skeletron": true,
    "split:boss-wall-of-flesh": true,
    "split:boss-destroyer": true,
    "split:boss-plantera": true,
    "split:boss-golem": true,
    "split:boss-lunatic-cultist": true,
    "split:boss-moon-lord": true
  },
  "SplitCompletionOutlineSplitStyles": {
    "split:boss-skeletron": "Gold",
    "split:boss-wall-of-flesh": "Gold",
    "split:boss-destroyer": "Rainbow",
    "split:boss-plantera": "Rainbow",
    "split:boss-golem": "Rainbow",
    "split:boss-lunatic-cultist": "Rainbow",
    "split:boss-moon-lord": "Rainbow"
  },
  "SplitCompletionOutlineSegmentStyles": {
    "split:boss-skeletron": "Aurora",
    "split:boss-wall-of-flesh": "Aurora",
    "split:boss-destroyer": "Aurora",
    "split:boss-plantera": "Aurora",
    "split:boss-golem": "Aurora",
    "split:boss-lunatic-cultist": "Aurora",
    "split:boss-moon-lord": "Aurora"
  },
  "ShowCurrentSplitHighlight": false,
  "CurrentSplitHighlightScalePercent": 112,
  "CurrentSplitDepthStrengthPercent": 45,
  "ShowEarlyDeltaTime": true,
  "EarlyDeltaTimeSeconds": 120,
  "EnableDynamicDeltaTimeUnits": true,
  "EnableDeltaGradientColor": true,
  "EnableCurrentDeltaGradientColor": true,
  "EnableTimerGradientColor": true,
  "DeltaGradientThresholdSeconds": 120,
  "DeltaGradientCurve": "SoftStep",
  "ShowSegmentBestDeltaHighlight": true,
  "SegmentBestDeltaHighlightStyles": {
    "split:boss-skeletron": "Aurora",
    "split:boss-wall-of-flesh": "Aurora",
    "split:boss-destroyer": "Aurora",
    "split:boss-plantera": "Aurora",
    "split:boss-golem": "Aurora",
    "split:boss-lunatic-cultist": "Aurora",
    "split:boss-moon-lord": "Aurora"
  },
  "Colors": {
    "ReferenceText": "#FFFFFF",
    "ReferenceTextOutline": "#101010",
    "ReferenceTextShadow": "#000000",
    "ActiveReferenceText": "#0075EC",
    "ActiveReferenceTextOutline": "#101010",
    "ActiveReferenceTextShadow": "#000000",
    "SplitText": "#F0A040",
    "SplitTextOutline": "#101010",
    "SplitTextShadow": "#000000",
    "DeltaAheadText": "#30FF30",
    "DeltaAheadTextOutline": "#101010",
    "DeltaAheadTextShadow": "#000000",
    "DeltaBehindText": "#FF3030",
    "DeltaBehindTextOutline": "#101010",
    "DeltaBehindTextShadow": "#000000",
    "TimerText": "#FFFFFF",
    "TimerTextOutline": "#101010",
    "TimerTextShadow": "#000000",
    "TimerAheadText": "#30FF30",
    "TimerAheadTextOutline": "#101010",
    "TimerAheadTextShadow": "#000000",
    "TimerBehindText": "#FF3030",
    "TimerBehindTextOutline": "#101010",
    "TimerBehindTextShadow": "#000000",
    "TimerRecordText": "#69A7FF",
    "TimerRecordTextOutline": "#101010",
    "TimerRecordTextShadow": "#000000",
    "TimerNoRecordText": "#FF0000",
    "TimerNoRecordTextOutline": "#101010",
    "TimerNoRecordTextShadow": "#000000",
    "TimerPausedText": "#5F5F5F",
    "TimerPausedTextOutline": "#101010",
    "TimerPausedTextShadow": "#000000",
    "SplitCompletionSegmentLabelText": "#DEDEE2",
    "SplitCompletionLabelText": "#DEDEE2",
    "SplitCompletionSegmentTimeText": "#FFFFFF",
    "SplitCompletionTimeText": "#FFFFFF"
  },
  "Sounds": {
    "Pause": "",
    "Resume": "",
    "Reset": "",
    "EnterWorld": "",
    "SplitBehindReferenceBehindSegment": "",
    "SplitBehindReferenceAheadSegment": "",
    "SplitAheadReferenceBehindSegment": "",
    "SplitAheadReferenceAheadSegment": "",
    "MoonLordBehindReferenceBehindSegment": "",
    "MoonLordBehindReferenceAheadSegment": "",
    "MoonLordAheadReferenceBehindSegment": "",
    "MoonLordAheadReferenceAheadSegment": ""
  },
  "Columns": {
    "ScalePercent": 100,
    "Icon": {
      "Show": true,
      "Width": 240,
      "FontFamily": "Segoe UI",
      "FontSize": 55,
      "Bold": false
    },
    "Time": {
      "Show": true,
      "Width": 180,
      "FontFamily": "Segoe UI",
      "FontSize": 18,
      "Bold": true
    },
    "Delta": {
      "Show": true,
      "Width": 200,
      "FontFamily": "Segoe UI",
      "FontSize": 18,
      "Bold": true
    },
    "AttachedIcon": {
      "Show": true,
      "Width": 240,
      "FontFamily": "Segoe UI",
      "FontSize": 55,
      "Bold": false
    },
    "AttachedTime": {
      "Show": true,
      "Width": 180,
      "FontFamily": "Segoe UI",
      "FontSize": 18,
      "Bold": true
    },
    "AttachedDelta": {
      "Show": true,
      "Width": 200,
      "FontFamily": "Segoe UI",
      "FontSize": 18,
      "Bold": true
    },
    "Timer": {
      "Show": true,
      "Width": 0,
      "FontFamily": "Segoe UI",
      "FontSize": 44,
      "Bold": true
    },
    "TimerMilliseconds": {
      "Show": true,
      "Width": 0,
      "FontFamily": "Segoe UI",
      "FontSize": 30,
      "Bold": true
    },
    "TimerOffsetX": 165,
    "TimerOffsetY": 0
  },
  "TextEffects": {
    "IconOpacityPercent": 100,
    "TimeOpacityPercent": 100,
    "TimeShadowPercent": 0,
    "TimeOutlineThicknessPercent": 100,
    "DeltaOpacityPercent": 100,
    "DeltaShadowPercent": 0,
    "DeltaOutlineThicknessPercent": 100,
    "AttachedIconOpacityPercent": 100,
    "AttachedTimeOpacityPercent": 100,
    "AttachedTimeShadowPercent": 0,
    "AttachedTimeOutlineThicknessPercent": 100,
    "AttachedDeltaOpacityPercent": 100,
    "AttachedDeltaShadowPercent": 0,
    "AttachedDeltaOutlineThicknessPercent": 100,
    "TimerOpacityPercent": 100,
    "TimerShadowPercent": 0,
    "TimerOutlineThicknessPercent": 100,
    "TimerMillisecondsOpacityPercent": 100,
    "TimerMillisecondsShadowPercent": 0,
    "TimerMillisecondsOutlineThicknessPercent": 100
  },
  "AutoCreate": {
    "PlayerName": "",
    "PlayerTemplateCode": "",
    "PlayerDifficulty": "Softcore",
    "WorldSize": "Small",
    "WorldDifficulty": "Classic",
    "WorldEvil": "Crimson",
    "SpecialSeeds": "",
    "SecretSeeds": "",
    "EnableZenithStarCatch": false,
    "ZenithStarCatchStopStage": "Pots",
    "ZenithStarCatchSpeedSliderValue": 411,
    "EnablePyramidFilter": false,
    "PyramidFilterItemMask": 3,
    "ReturnToMainMenuOnFilterFailure": false,
    "EnableWorldPool": false,
    "WorldPoolTargetCount": 10,
    "ShortActionDelayMilliseconds": 10,
    "MenuActionDelayMilliseconds": 90,
    "PyramidFilterPostDelayMilliseconds": 50,
    "WindowActivationDelayMilliseconds": 10,
    "ClickFocusDelayMilliseconds": 10,
    "InputPressDurationMilliseconds": 140
  },
  "PracticeWorlds": {
    "Slots": [
      {
        "Name": "",
        "PlayerFilePath": "",
        "WorldFilePath": ""
      },
      {
        "Name": "",
        "PlayerFilePath": "",
        "WorldFilePath": ""
      },
      {
        "Name": "",
        "PlayerFilePath": "",
        "WorldFilePath": ""
      },
      {
        "Name": "",
        "PlayerFilePath": "",
        "WorldFilePath": ""
      },
      {
        "Name": "",
        "PlayerFilePath": "",
        "WorldFilePath": ""
      },
      {
        "Name": "",
        "PlayerFilePath": "",
        "WorldFilePath": ""
      },
      {
        "Name": "",
        "PlayerFilePath": "",
        "WorldFilePath": ""
      },
      {
        "Name": "",
        "PlayerFilePath": "",
        "WorldFilePath": ""
      },
      {
        "Name": "",
        "PlayerFilePath": "",
        "WorldFilePath": ""
      },
      {
        "Name": "",
        "PlayerFilePath": "",
        "WorldFilePath": ""
      }
    ]
  },
  "Advanced": {
    "EnableTerrariaUiScalePatch": false,
    "ReadyWatcherPollHz": 240,
    "ReadyUiControlHz": 120,
    "RunningStatusPaintHz": 120,
    "TimerOverlayRefreshHz": 120
  },
  "EnableDefeatedBossIconLighting": true,
  "UndefeatedIconGrayscalePercent": 80,
  "UndefeatedIconBrightnessPercent": 40,
  "CurrentBossIconGrayscaleWeakenPercent": 0,
  "CurrentBossIconBrightnessBoostPercent": 0
}
""";

    public const string ReferenceTimesWrJson = """
{
  "Name": "WR",
  "Splits": {
    "condition:split:biome-underworld:complete": "10:25.00",
    "condition:split:boss-skeletron:boss-skeletron-defeated-istrue-1": "13:42.00",
    "condition:split:boss-wall-of-flesh:boss-wall-of-flesh-defeated-istrue-1": "16:13.00",
    "condition:split:item-525:complete": "22:20.00",
    "condition:split:boss-destroyer:boss-destroyer-defeated-istrue-1": "29:41.00",
    "condition:split:boss-destroyer:boss-twins-defeated-istrue-1": "32:07.00",
    "condition:split:boss-destroyer:boss-skeletron-prime-defeated-istrue-1": "31:22.00",
    "condition:split:boss-plantera:boss-plantera-defeated-istrue-1": "35:14.00",
    "condition:split:boss-golem:boss-golem-defeated-istrue-1": "37:01.00",
    "condition:split:boss-lunatic-cultist:boss-lunatic-cultist-defeated-istrue-1": "38:52.00",
    "condition:split:boss-moon-lord:boss-moon-lord-defeated-istrue-1": "48:12.63",
    "condition:split:item-3601:complete": "46:53.70",
    "condition:split:item-3459:complete": "41:44.22",
    "condition:split:item-520:complete": "23:52.00",
    "condition:split:biome-aether:complete": "26:20.00",
    "condition:split:biome-jungle:complete": "3:11.00",
    "condition:split:item-167:complete": "6:50.00",
    "condition:split:item-857:complete": "0:51.00"
  }
}
""";
}
