
/* how its gonna work
 load in workspace
    get semantic modal from comp
    
    look forall classes that implement ApiController attribute
        - get all methods that implement http attributes
            - get route info
            - get parameters for methods
            - get return type
    need a function/class/repo that can
        - take a class definition that uses ApiController
        - goes through methods
        - print out api endpoints 
    need a function/class/repo that can 
        - takes a Class definition 
        - take properties and there types and print a string of them
                - for starters: Name | Type | Nullable
                
                
so a couple phases 
    1 get solution compliation |
        - a compilation
    2 get all class defs with api controller  ||
        - a list of ...
            - the class syntax, 
            - the attributes and there args for class
                - endpoint name/base
            - semanticModels of class's
    3 get all functions that have api connections ||
        - a list of methods: 
            - return types, 
            - argument types, 
            - attribute args
                - endpoint extention/route/method type
    4 get all types ||
        - name of class
        - name of property
        - simple type or reffrence to other type(im seeing a class here)
*|| means in parallel*
*/


// See https://aka.ms/new-console-template for more information
//perfect example
//https://dev.to/mattjhosking/analysing-a-net-codebase-with-roslyn-5cn0

using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.VisualBasic;

var workspace = MSBuildWorkspace.Create();
workspace.SkipUnrecognizedProjects = true;
workspace.WorkspaceFailed += (sender, args) =>
{
    if (args.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
    {
        Console.Error.WriteLine(args.Diagnostic.Message);
    }
};

//TODO take a location as input
var solution = await workspace.OpenSolutionAsync("../../../../Stuby.sln",new ProgressBarProjectLoadStatus());
var aspNetProjects = solution.Projects
    .Where(project => project.MetadataReferences
        .Any(reference => reference.Display != null &&
                          reference.Display.Contains("Microsoft.AspNetCore",
                              StringComparison.OrdinalIgnoreCase)))
    .ToList(); 
//skipping ourselves just for testing
var comp = await aspNetProjects.Skip(1).First().GetCompilationAsync();
var classDef = comp!.SyntaxTrees
    .SelectMany(s => s.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>());
var apiControllers = classDef.Where(cd =>
    cd.AttributeLists.Any(a => a.Attributes.Any(an => an.Name.ToString() == "ApiController")));
var classDeclarationSyntaxes = apiControllers.ToList();

//getting class and the models
ClassDeclarationSyntax classSyntax = classDeclarationSyntaxes.First();
SemanticModel model = comp.GetSemanticModel(classSyntax.SyntaxTree);
INamedTypeSymbol? myClass = model.GetDeclaredSymbol(classSyntax);

//getting the endpoint
var routeAttrib = myClass!.GetAttributes().First(a => a.AttributeClass!.Name ==nameof(RouteAttribute));
var routeName = (string)routeAttrib.ConstructorArguments.First().Value!;
Console.WriteLine($"endpoint: {routeName.Replace("[controller]",myClass.Name.Replace("Controller","",StringComparison.OrdinalIgnoreCase))}");

string[] attributeNames =
[
    nameof(HttpGetAttribute), nameof(HttpPostAttribute), nameof(HttpPutAttribute), nameof(HttpDeleteAttribute)
];
var methods = myClass.GetMembers()
    .OfType<IMethodSymbol>()
    .Where(m => m.GetAttributes()
        .Any(a => attributeNames.Contains(a.AttributeClass?.Name) ));
var request =methods.First().Parameters;
var noRequestParams = request.Length == 0;
Console.WriteLine($"{methods.First().Name}, {(noRequestParams ? "Takes no Arguments" : "Somethings there")} ");

var returnType = methods.First().ReturnType;
//TODO do this for request as well
INamedTypeSymbol? responseType = null;
// Check if the return type is an IEnumerable
if (returnType is INamedTypeSymbol { IsGenericType: true } namedTypeSymbol )
{
    //only supports 1 generic right now
    responseType = (INamedTypeSymbol)namedTypeSymbol.TypeArguments.First();
}
else if(returnType is INamedTypeSymbol type)
{
    responseType = type;
}
//TODO check if return Type is ITypedSymbol (these are basic types)
if (returnType != null)
{
    Console.WriteLine($"returns {returnType.Name}");
    IEnumerable<(string ReturnType, string Name)> types = responseType!
        .GetMembers()
        .OfType<IPropertySymbol>()
        .Where(p => p.GetMethod != null)
        .Select(p => (p.GetMethod!.ReturnType.Name, p.Name));
    foreach (var valueTuple in types)
    {
       Console.WriteLine(valueTuple.ToString()); 
    }
}
Console.WriteLine("hi");

class ClassState
{
    private ClassDeclarationSyntax _Syntax;
    private SemanticModel _Model;
    private INamespaceSymbol _Declaratation;

    public string ControllerName;
    public string RouteName;

    void GetRouteName()
    {
        //TODO make a strat for this
        var routeAttrib = _Declaratation.GetAttributes().First(a => a.AttributeClass!.Name ==nameof(RouteAttribute));
        var routeName = (string)routeAttrib.ConstructorArguments.First().Value!;
        RouteName = routeName.Replace("[controller]",
            _Declaratation.Name.Replace("Controller", "", 
                StringComparison.OrdinalIgnoreCase));
    }
}

class MethodState
{
    private INamespaceSymbol _Declaratation;

    private readonly string[] _attributeNames =
    [
        nameof(HttpGetAttribute), nameof(HttpPostAttribute), nameof(HttpPutAttribute), nameof(HttpDeleteAttribute)
    ];

    private IEnumerable<IMethodSymbol> Methods;
    void GetMethods()
    {
        Methods = _Declaratation.GetMembers().OfType<IMethodSymbol>()
            .Where(m => m.GetAttributes()
                .Any(a => _attributeNames.Contains(a.AttributeClass?.Name) ));
 
    }
}

public class ProgressBarProjectLoadStatus : IProgress<ProjectLoadProgress>
{
    public void Report(ProjectLoadProgress value)
    {
       Console.Out.WriteLine($"{value.Operation} {value.FilePath}");
    }
}

//represents the type data that we care about
public struct TypeData(string PropertyName, string TypeName);
public class Endpoint
{
    public Endpoint(string raw)
    {
        Raw = raw;
        Args = new Dictionary<string, TypeData?>();
        //TODO fill with all {param}
    }

    public void SetTypeInfo(string name,ITypeSymbol type)
    {
        if (Args.ContainsKey(name))
        {
            Args[name] = new TypeData(name,type.Name);
        }
    }

    public readonly string Raw; 
    public Dictionary<string,TypeData?> Args { get; }
};