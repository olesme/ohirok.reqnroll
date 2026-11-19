// Modified from Reqnroll (https://github.com/reqnroll/Reqnroll)
// Original code licensed under BSD 3‑Clause License.

using Reqnroll.Generator.UnitTestConverter;
using Reqnroll.Parser;

namespace Ohirok.GenerationInterceptor.Generation
{
    #region INTERCEPT 
    //SHADOW COPY of Reqnroll.Generator.Generation.UnitTestFeatureGeneratorProvider
    public class UnitTestFeatureGeneratorProviderInt : IFeatureGeneratorProvider
    {
        private readonly UnitTestFeatureGeneratorInt _unitTestFeatureGenerator;

        public UnitTestFeatureGeneratorProviderInt(UnitTestFeatureGeneratorInt unitTestFeatureGenerator)
        #endregion
        {
            _unitTestFeatureGenerator = unitTestFeatureGenerator;
        }

        public int Priority => PriorityValues.Lowest;

        public bool CanGenerate(ReqnrollDocument document)
        {
            return true;
        }

        public IFeatureGenerator CreateGenerator(ReqnrollDocument document)
        {
            return _unitTestFeatureGenerator;
        }
    }
}
