namespace VaultMcp.Tools.KnowledgeBase.Search.Lexical;

internal sealed record LexicalSearchScoringOptions
{
    public static LexicalSearchScoringOptions Default { get; } = new()
    {
        ExcerptRadius = 80,
        Path = new TextMatchScoringOptions
        {
            ExactMatchBoost = 400,
            ContainsBoost = 120,
            ExactPhraseBoost = 80,
            PerTermBoost = 20
        },
        SearchNotes = new SearchNotesScoringOptions
        {
            Title = new TextMatchScoringOptions
            {
                ExactMatchBoost = 900,
                StartsWithBoost = 500,
                ContainsBoost = 200,
                ExactPhraseBoost = 240,
                PerTermBoost = 60
            },
            Headings = new ListMatchScoringOptions
            {
                ExactMatchBoost = 520,
                ContainsBoost = 180,
                ExactPhraseBoost = 220,
                PerTermBoost = 70
            },
            Aliases = new ListMatchScoringOptions
            {
                ExactMatchBoost = 760,
                ContainsBoost = 240,
                ExactPhraseBoost = 260,
                PerTermBoost = 85
            },
            Tags = new ListMatchScoringOptions
            {
                ExactMatchBoost = 300,
                ContainsBoost = 140,
                ExactPhraseBoost = 120,
                PerTermBoost = 55
            },
            Kind = new SingleValueMatchScoringOptions
            {
                ExactMatchBoost = 160,
                ContainsBoost = 60,
                ExactPhraseBoost = 30
            },
            Content = new ContentMatchScoringOptions
            {
                ContainsBoost = 30,
                ExactPhraseBoost = 110,
                PerOccurrenceBoost = 6,
                PerTermBoost = 6
            }
        },
        FindTerm = new FindTermScoringOptions
        {
            Title = new TextMatchScoringOptions
            {
                ExactMatchBoost = 1200,
                StartsWithBoost = 700,
                ContainsBoost = 350,
                ExactPhraseBoost = 260
            },
            FileName = new TextMatchScoringOptions
            {
                ExactMatchBoost = 900,
                ContainsBoost = 250
            },
            Aliases = new ListMatchScoringOptions
            {
                ExactMatchBoost = 1120,
                ContainsBoost = 360,
                ExactPhraseBoost = 280,
                PerTermBoost = 95
            },
            Headings = new ListMatchScoringOptions
            {
                ExactMatchBoost = 420,
                ContainsBoost = 180,
                ExactPhraseBoost = 180,
                PerTermBoost = 60
            },
            Tags = new ListMatchScoringOptions
            {
                ExactMatchBoost = 220,
                ContainsBoost = 100,
                ExactPhraseBoost = 100,
                PerTermBoost = 40
            },
            Kind = new SingleValueMatchScoringOptions
            {
                ExactMatchBoost = 120,
                ContainsBoost = 40,
                ExactPhraseBoost = 20
            },
            TermKindBoost = 1800,
            ExactAliasOnTermBoost = 500,
            Content = new ContentMatchScoringOptions
            {
                ExactMatchBoost = 150,
                ContainsBoost = 40,
                ExactPhraseBoost = 120,
                PerOccurrenceBoost = 8,
                PerTermBoost = 8
            }
        },
        RelatedNotes = new RelatedNoteScoringOptions
        {
            ExplicitRelatedBoost = 2400,
            MaxSharedTerms = 6,
            SharedTermBoost = 90,
            SharedTermInPathBoost = 20,
            SameDirectoryBoost = 120,
            SharedTagBoost = 180
        },
        KindAffinity = new KindAffinityScoringOptions
        {
            SameKindBoost = 120,
            WorkflowToInvariantBoost = 220,
            WorkflowToDecisionBoost = 180,
            WorkflowToDataFlowBoost = 180,
            InvariantToWorkflowBoost = 200,
            InvariantToDecisionBoost = 140,
            DecisionToWorkflowBoost = 140,
            DecisionToInvariantBoost = 140,
            DataFlowToWorkflowBoost = 180,
            DataFlowToInvariantBoost = 120
        }
    };

    public required int ExcerptRadius { get; init; }
    public required TextMatchScoringOptions Path { get; init; }
    public required SearchNotesScoringOptions SearchNotes { get; init; }
    public required FindTermScoringOptions FindTerm { get; init; }
    public required RelatedNoteScoringOptions RelatedNotes { get; init; }
    public required KindAffinityScoringOptions KindAffinity { get; init; }
}

internal sealed record TextMatchScoringOptions
{
    public required int ExactMatchBoost { get; init; }
    public int StartsWithBoost { get; init; }
    public int ContainsBoost { get; init; }
    public int ExactPhraseBoost { get; init; }
    public int PerTermBoost { get; init; }
}

internal sealed record ListMatchScoringOptions
{
    public required int ExactMatchBoost { get; init; }
    public required int ContainsBoost { get; init; }
    public required int ExactPhraseBoost { get; init; }
    public required int PerTermBoost { get; init; }
}

internal sealed record SingleValueMatchScoringOptions
{
    public required int ExactMatchBoost { get; init; }
    public required int ContainsBoost { get; init; }
    public required int ExactPhraseBoost { get; init; }
}

internal sealed record ContentMatchScoringOptions
{
    public int ExactMatchBoost { get; init; }
    public required int ContainsBoost { get; init; }
    public required int ExactPhraseBoost { get; init; }
    public required int PerOccurrenceBoost { get; init; }
    public required int PerTermBoost { get; init; }
}

internal sealed record SearchNotesScoringOptions
{
    public required TextMatchScoringOptions Title { get; init; }
    public required ListMatchScoringOptions Headings { get; init; }
    public required ListMatchScoringOptions Aliases { get; init; }
    public required ListMatchScoringOptions Tags { get; init; }
    public required SingleValueMatchScoringOptions Kind { get; init; }
    public required ContentMatchScoringOptions Content { get; init; }
}

internal sealed record FindTermScoringOptions
{
    public required TextMatchScoringOptions Title { get; init; }
    public required TextMatchScoringOptions FileName { get; init; }
    public required ListMatchScoringOptions Aliases { get; init; }
    public required ListMatchScoringOptions Headings { get; init; }
    public required ListMatchScoringOptions Tags { get; init; }
    public required SingleValueMatchScoringOptions Kind { get; init; }
    public required int TermKindBoost { get; init; }
    public required int ExactAliasOnTermBoost { get; init; }
    public required ContentMatchScoringOptions Content { get; init; }
}

internal sealed record RelatedNoteScoringOptions
{
    public required int ExplicitRelatedBoost { get; init; }
    public required int MaxSharedTerms { get; init; }
    public required int SharedTermBoost { get; init; }
    public required int SharedTermInPathBoost { get; init; }
    public required int SameDirectoryBoost { get; init; }
    public required int SharedTagBoost { get; init; }
}

internal sealed record KindAffinityScoringOptions
{
    public required int SameKindBoost { get; init; }
    public required int WorkflowToInvariantBoost { get; init; }
    public required int WorkflowToDecisionBoost { get; init; }
    public required int WorkflowToDataFlowBoost { get; init; }
    public required int InvariantToWorkflowBoost { get; init; }
    public required int InvariantToDecisionBoost { get; init; }
    public required int DecisionToWorkflowBoost { get; init; }
    public required int DecisionToInvariantBoost { get; init; }
    public required int DataFlowToWorkflowBoost { get; init; }
    public required int DataFlowToInvariantBoost { get; init; }
}
