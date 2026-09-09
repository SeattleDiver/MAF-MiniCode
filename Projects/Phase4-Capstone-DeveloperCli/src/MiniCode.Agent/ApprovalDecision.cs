namespace MiniCode.Agent;

/// <summary>What the operator decided about one pending tool call.</summary>
public enum ApprovalDecision
{
    /// <summary>Run this call, and ask again next time.</summary>
    Approve,

    /// <summary>Refuse this call. The model is told, in words, that it was not approved.</summary>
    Deny,

    /// <summary>Run this call, and stop asking for the rest of the session.</summary>
    AlwaysApprove,
}
