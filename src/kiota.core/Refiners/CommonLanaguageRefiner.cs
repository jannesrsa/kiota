using System;
using System.Linq;

namespace kiota.core {
    public abstract class CommonLanguageRefiner : ILanguageRefiner
    {
        public abstract void Refine(CodeNamespace generatedCode);

        internal void AddInnerClasses(CodeElement current) {
            if(current is CodeClass currentClass) {
                foreach(var parameter in current.GetChildElements().OfType<CodeMethod>().SelectMany(x =>x.Parameters).Where(x => x.Type.ActionOf))
                    currentClass.AddInnerClass(parameter.Type.TypeDefinition);
            }
            CrawlTree(current, AddInnerClasses);
        }
        private readonly CodeUsingComparer usingComparerWithDeclarations = new CodeUsingComparer(true);
        private readonly CodeUsingComparer usingComparerWithoutDeclarations = new CodeUsingComparer(false);
        protected void AddPropertiesAndMethodTypesImports(CodeElement current, bool includeParentNamespaces, bool includeCurrentNamespace, bool compareOnDeclaration) {
            if(current is CodeClass currentClass) {
                var currentClassNamespace = currentClass.GetImmediateParentOfType<CodeNamespace>();
                var propertiesTypes = currentClass
                                    .InnerChildElements
                                    .OfType<CodeProperty>()
                                    .Where(x => x.PropertyKind != CodePropertyKind.ResponseHandler)
                                    .Select(x => x.Type)
                                    .Distinct();
                var methods = currentClass
                                    .InnerChildElements
                                    .OfType<CodeMethod>()
                                    .Where(x => x.MethodKind != CodeMethodKind.ResponseHandler);
                var methodsReturnTypes = methods
                                    .Select(x => x.ReturnType)
                                    .Distinct();
                var methodsParametersTypes = methods
                                    .SelectMany(x => x.Parameters)
                                    .Where(x => x.ParameterKind == CodeParameterKind.Custom)
                                    .Select(x => x.Type)
                                    .Distinct();
                var indexerTypes = currentClass
                                    .InnerChildElements
                                    .OfType<CodeIndexer>()
                                    .Select(x => x.ReturnType)
                                    .Distinct();
                var usingsToAdd = propertiesTypes
                                    .Union(methodsParametersTypes)
                                    .Union(methodsReturnTypes)
                                    .Union(indexerTypes)
                                    .Select(x => new Tuple<CodeType, CodeNamespace>(x, x?.TypeDefinition?.GetImmediateParentOfType<CodeNamespace>()))
                                    .Where(x => x.Item2 != null && (includeCurrentNamespace || x.Item2 != currentClassNamespace))
                                    .Where(x => includeParentNamespaces || !currentClassNamespace.IsChildOf(x.Item2))
                                    .Select(x => new CodeUsing(currentClass) { Name = x.Item2.Name, Declaration = x.Item1 })
                                    .Distinct(compareOnDeclaration ? usingComparerWithDeclarations : usingComparerWithoutDeclarations)
                                    .ToArray();
                if(usingsToAdd.Any())
                    currentClass.AddUsing(usingsToAdd);
            }
            CrawlTree(current, (x) => AddPropertiesAndMethodTypesImports(x, includeParentNamespaces, includeCurrentNamespace, compareOnDeclaration));
        }
        protected void CrawlTree(CodeElement currentElement, Action<CodeElement> function) {
            foreach(var childElement in currentElement.GetChildElements())
                function.Invoke(childElement);
        }
    }
}
