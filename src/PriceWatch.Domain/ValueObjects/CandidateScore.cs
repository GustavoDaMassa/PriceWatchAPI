using PriceWatch.Domain.Enums;

namespace PriceWatch.Domain.ValueObjects;

public record CandidateScore(bool Accepted, double Score, CandidateReason Reason);
