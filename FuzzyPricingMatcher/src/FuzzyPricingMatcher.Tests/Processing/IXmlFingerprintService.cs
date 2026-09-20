using System.Xml.Linq;

namespace FuzzyPricingMatcher.Tests.Processing;

public interface IXmlFingerprintService
{
    string CreateFingerprint(XDocument document);
}