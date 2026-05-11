namespace Lux.IR;

internal partial class IRVisitor
{
    public override Node VisitFunctionDecl(LuxParser.FunctionDeclContext context)
    {
        var (namePath, methodName) = VisitFuncNameContent(context.funcName());
        var (parameters, returnType, body, ret) = VisitFuncBodyContent(context.funcBody());
        var isAsync = context.ASYNC() != null;
        var typeParams = VisitTypeParamListContent(context.funcBody().typeParamList());
        var decl = new FunctionDecl(NewNodeID, SpanFromCtx(context), namePath, methodName, parameters, returnType, body, ret, isAsync);
        decl.TypeParams = typeParams;
        decl.Annotations = VisitAnnotationListContent(context.annotationList());
        return decl;
    }

    public override Node VisitLocalFunctionDecl(LuxParser.LocalFunctionDeclContext context)
    {
        var (parameters, returnType, body, ret) = VisitFuncBodyContent(context.funcBody());
        var isAsync = context.ASYNC() != null;
        var typeParams = VisitTypeParamListContent(context.funcBody().typeParamList());
        var decl = new LocalFunctionDecl(NewNodeID, SpanFromCtx(context), NameRefFromTerm(context.NAME()), parameters, returnType, body, ret, isAsync);
        decl.TypeParams = typeParams;
        decl.Annotations = VisitAnnotationListContent(context.annotationList());
        return decl;
    }

    public override Node VisitLocalDecl(LuxParser.LocalDeclContext context)
    {
        var vars = VisitAttribNameListContent(context.attribNameList());
        var values = context.exprList()?.expr().Select(e => (Expr)Visit(e)).ToList() ?? [];
        var isMutable = context.MUT() != null;
        var decl = new LocalDecl(NewNodeID, SpanFromCtx(context), vars, values, isMutable);
        decl.Annotations = VisitAnnotationListContent(context.annotationList());
        return decl;
    }

    public override Node VisitDeclareStat(LuxParser.DeclareStatContext context)
        => Visit(context.declareBody());

    public override Node VisitDeclareFunction(LuxParser.DeclareFunctionContext context)
    {
        var (namePath, methodName) = VisitFuncNameContent(context.funcName());
        var (parameters, returnType) = VisitFuncSignatureContent(context.funcSignature());
        var isAsync = context.ASYNC() != null;
        var typeParams = VisitTypeParamListContent(context.funcSignature().typeParamList());
        var decl = new DeclareFunctionDecl(NewNodeID, SpanFromCtx(context), namePath, methodName, parameters, returnType, isAsync);
        decl.TypeParams = typeParams;
        return decl;
    }

    public override Node VisitDeclareVariable(LuxParser.DeclareVariableContext context)
    {
        var typeRef = (TypeRef)Visit(context.typeAnnotation().typeExpr());
        return new DeclareVariableDecl(NewNodeID, SpanFromCtx(context), NameRefFromTerm(context.NAME()), typeRef);
    }

    public override Node VisitDeclareModule(LuxParser.DeclareModuleContext context)
    {
        var moduleName = NameRefFromString(context.str());
        var members = new List<Decl>();

        foreach (var member in context.declareModuleBlock().declareModuleMember())
            members.Add((Decl)Visit(member));

        return new DeclareModuleDecl(NewNodeID, SpanFromCtx(context), moduleName, members);
    }

    public override Node VisitModuleDeclareFunction(LuxParser.ModuleDeclareFunctionContext context)
    {
        var (namePath, methodName) = VisitFuncNameContent(context.funcName());
        var (parameters, returnType) = VisitFuncSignatureContent(context.funcSignature());
        var isAsync = context.ASYNC() != null;
        var typeParams = VisitTypeParamListContent(context.funcSignature().typeParamList());
        var decl = new DeclareFunctionDecl(NewNodeID, SpanFromCtx(context), namePath, methodName, parameters, returnType, isAsync);
        decl.TypeParams = typeParams;
        return decl;
    }

    public override Node VisitModuleDeclareVariable(LuxParser.ModuleDeclareVariableContext context)
    {
        var typeRef = (TypeRef)Visit(context.typeAnnotation().typeExpr());
        return new DeclareVariableDecl(NewNodeID, SpanFromCtx(context), NameRefFromTerm(context.NAME()), typeRef);
    }

    public override Node VisitEnumDecl(LuxParser.EnumDeclContext context)
    {
        var name = NameRefFromTerm(context.NAME());
        var members = new List<EnumMember>();
        foreach (var memberCtx in context.enumMember())
        {
            var memberName = NameRefFromTerm(memberCtx.NAME());
            Expr? value = memberCtx.expr() != null ? (Expr)Visit(memberCtx.expr()) : null;
            var member = new EnumMember(memberName, value, null, SpanFromCtx(memberCtx));
            member.Annotations = VisitAnnotationListContent(memberCtx.annotationList());
            members.Add(member);
        }
        var enumDecl = new EnumDecl(NewNodeID, SpanFromCtx(context), name, members, isDeclare: false);
        enumDecl.Annotations = VisitAnnotationListContent(context.annotationList());
        return enumDecl;
    }

    public override Node VisitDeclareEnum(LuxParser.DeclareEnumContext context)
    {
        var name = NameRefFromTerm(context.NAME());
        var members = new List<EnumMember>();
        foreach (var memberCtx in context.declareEnumMember())
        {
            var memberName = NameRefFromTerm(memberCtx.NAME());
            TypeRef? typeAnn = memberCtx.typeAnnotation() != null
                ? (TypeRef)Visit(memberCtx.typeAnnotation().typeExpr())
                : null;
            members.Add(new EnumMember(memberName, null, typeAnn, SpanFromCtx(memberCtx)));
        }
        return new EnumDecl(NewNodeID, SpanFromCtx(context), name, members, isDeclare: true);
    }

    public override Node VisitModuleDeclareEnum(LuxParser.ModuleDeclareEnumContext context)
    {
        var name = NameRefFromTerm(context.NAME());
        var members = new List<EnumMember>();
        foreach (var memberCtx in context.declareEnumMember())
        {
            var memberName = NameRefFromTerm(memberCtx.NAME());
            TypeRef? typeAnn = memberCtx.typeAnnotation() != null
                ? (TypeRef)Visit(memberCtx.typeAnnotation().typeExpr())
                : null;
            members.Add(new EnumMember(memberName, null, typeAnn, SpanFromCtx(memberCtx)));
        }
        return new EnumDecl(NewNodeID, SpanFromCtx(context), name, members, isDeclare: true);
    }

    public override Node VisitDeclareClass(LuxParser.DeclareClassContext context)
    {
        var name = NameRefFromTerm(context.NAME());
        var classRefs = context.classRef();

        NameRef? baseClass = null;
        var baseClassTypeArgs = new List<TypeArgRef>();
        var interfaces = new List<NameRef>();
        var interfaceTypeArgs = new List<List<TypeArgRef>>();

        var refIdx = 0;
        if (context.EXTENDS() != null && classRefs.Length > refIdx)
        {
            var (bcName, bcArgs) = VisitClassRefContent(classRefs[refIdx]);
            baseClass = bcName;
            baseClassTypeArgs = bcArgs;
            refIdx++;
        }

        if (context.IMPLEMENTS() != null)
        {
            for (var i = refIdx; i < classRefs.Length; i++)
            {
                var (iName, iArgs) = VisitClassRefContent(classRefs[i]);
                interfaces.Add(iName);
                interfaceTypeArgs.Add(iArgs);
            }
        }

        var typeParams = VisitTypeParamListContent(context.typeParamList());

        var fields = new List<ClassFieldNode>();
        var methods = new List<ClassMethodNode>();
        ClassConstructorNode? constructor = null;
        var accessors = new List<ClassAccessorNode>();

        foreach (var member in context.declareClassMember())
        {
            switch (member)
            {
                case LuxParser.DeclareClassFieldMemberContext field:
                {
                    var isLocal = field.LOCAL() != null;
                    var isStatic = field.STATIC() != null;
                    var isProtected = field.PROTECTED() != null;
                    var fieldName = NameRefFromTerm(field.NAME());
                    TypeRef? typeAnn = field.typeAnnotation() != null
                        ? (TypeRef)Visit(field.typeAnnotation().typeExpr())
                        : null;
                    fields.Add(new ClassFieldNode(fieldName, typeAnn, null, isLocal, isStatic, isProtected, SpanFromCtx(field)));
                    break;
                }
                case LuxParser.DeclareClassMethodMemberContext method:
                {
                    var isLocal = method.LOCAL() != null;
                    var isStatic = method.STATIC() != null;
                    var isAsync = method.ASYNC() != null;
                    var isProtected = method.PROTECTED() != null;
                    var isOverride = method.OVERRIDE() != null;
                    var isAbstract = method.ABSTRACT() != null;
                    var methodName = NameRefFromTerm(method.NAME());
                    var (parameters, returnType) = VisitFuncSignatureContent(method.funcSignature());
                    var methodTypeParams = VisitTypeParamListContent(method.funcSignature().typeParamList());
                    var cmNode = new ClassMethodNode(methodName, parameters, returnType, [], null, isLocal, isStatic, isAsync, isProtected, isOverride, isAbstract, SpanFromCtx(method));
                    cmNode.TypeParams = methodTypeParams;
                    methods.Add(cmNode);
                    break;
                }
                case LuxParser.DeclareClassConstructorMemberContext ctor:
                {
                    var (parameters, _) = VisitFuncSignatureContent(ctor.funcSignature());
                    constructor = new ClassConstructorNode(parameters, [], null, SpanFromCtx(ctor));
                    break;
                }
                case LuxParser.DeclareClassAccessorMemberContext accessor:
                {
                    var kindName = accessor.NAME(0).GetText();
                    var propName = NameRefFromTerm(accessor.NAME(1));
                    var kind = kindName == "get" ? AccessorKind.Getter : AccessorKind.Setter;
                    var (parameters, returnType) = VisitFuncSignatureContent(accessor.funcSignature());
                    accessors.Add(new ClassAccessorNode(kind, propName, parameters, returnType, [], null, false, SpanFromCtx(accessor)));
                    break;
                }
            }
        }

        var isClassAbstract = context.ABSTRACT() != null;
        var decl = new ClassDecl(NewNodeID, SpanFromCtx(context), name, baseClass, interfaces, fields, methods, constructor, accessors, isDeclare: true, isAbstract: isClassAbstract);
        decl.TypeParams = typeParams;
        decl.BaseClassTypeArgs = baseClassTypeArgs;
        decl.InterfaceTypeArgs = interfaceTypeArgs;
        return decl;
    }

    public override Node VisitDeclareInterface(LuxParser.DeclareInterfaceContext context)
    {
        var name = NameRefFromTerm(context.NAME());
        var classRefs = context.classRef();

        var baseInterfaces = new List<NameRef>();
        var baseInterfaceTypeArgs = new List<List<TypeArgRef>>();
        if (context.EXTENDS() != null)
        {
            foreach (var cr in classRefs)
            {
                var (iName, iArgs) = VisitClassRefContent(cr);
                baseInterfaces.Add(iName);
                baseInterfaceTypeArgs.Add(iArgs);
            }
        }

        var typeParams = VisitTypeParamListContent(context.typeParamList());

        var fields = new List<InterfaceFieldNode>();
        var methods = new List<InterfaceMethodNode>();

        foreach (var member in context.interfaceMember())
        {
            switch (member)
            {
                case LuxParser.InterfaceFieldMemberContext field:
                {
                    var fieldName = NameRefFromTerm(field.NAME());
                    var typeAnn = (TypeRef)Visit(field.typeAnnotation().typeExpr());
                    fields.Add(new InterfaceFieldNode(fieldName, typeAnn, SpanFromCtx(field)));
                    break;
                }
                case LuxParser.InterfaceMethodMemberContext method:
                {
                    var isAsync = method.ASYNC() != null;
                    var methodName = NameRefFromTerm(method.NAME());
                    var (parameters, returnType) = VisitFuncSignatureContent(method.funcSignature());
                    var imTypeParams = VisitTypeParamListContent(method.funcSignature().typeParamList());
                    var imNode = new InterfaceMethodNode(methodName, parameters, returnType, isAsync, SpanFromCtx(method));
                    imNode.TypeParams = imTypeParams;
                    methods.Add(imNode);
                    break;
                }
            }
        }

        var ifaceDecl = new InterfaceDecl(NewNodeID, SpanFromCtx(context), name, baseInterfaces, fields, methods, isDeclare: true);
        ifaceDecl.TypeParams = typeParams;
        ifaceDecl.BaseInterfaceTypeArgs = baseInterfaceTypeArgs;
        return ifaceDecl;
    }

    public override Node VisitModuleDeclareClass(LuxParser.ModuleDeclareClassContext context)
    {
        var name = NameRefFromTerm(context.NAME());
        var classRefs = context.classRef();

        NameRef? baseClass = null;
        var baseClassTypeArgs = new List<TypeArgRef>();
        var interfaces = new List<NameRef>();
        var interfaceTypeArgs = new List<List<TypeArgRef>>();

        var refIdx = 0;
        if (context.EXTENDS() != null && classRefs.Length > refIdx)
        {
            var (bcName, bcArgs) = VisitClassRefContent(classRefs[refIdx]);
            baseClass = bcName;
            baseClassTypeArgs = bcArgs;
            refIdx++;
        }

        if (context.IMPLEMENTS() != null)
        {
            for (var i = refIdx; i < classRefs.Length; i++)
            {
                var (iName, iArgs) = VisitClassRefContent(classRefs[i]);
                interfaces.Add(iName);
                interfaceTypeArgs.Add(iArgs);
            }
        }

        var typeParams = VisitTypeParamListContent(context.typeParamList());

        var fields = new List<ClassFieldNode>();
        var methods = new List<ClassMethodNode>();
        ClassConstructorNode? constructor = null;
        var accessors = new List<ClassAccessorNode>();

        foreach (var member in context.declareClassMember())
        {
            switch (member)
            {
                case LuxParser.DeclareClassFieldMemberContext field:
                {
                    var isLocal = field.LOCAL() != null;
                    var isStatic = field.STATIC() != null;
                    var isProtected = field.PROTECTED() != null;
                    var fieldName = NameRefFromTerm(field.NAME());
                    TypeRef? typeAnn = field.typeAnnotation() != null
                        ? (TypeRef)Visit(field.typeAnnotation().typeExpr())
                        : null;
                    fields.Add(new ClassFieldNode(fieldName, typeAnn, null, isLocal, isStatic, isProtected, SpanFromCtx(field)));
                    break;
                }
                case LuxParser.DeclareClassMethodMemberContext method:
                {
                    var isLocal = method.LOCAL() != null;
                    var isStatic = method.STATIC() != null;
                    var isAsync = method.ASYNC() != null;
                    var isProtected = method.PROTECTED() != null;
                    var isOverride = method.OVERRIDE() != null;
                    var isAbstract = method.ABSTRACT() != null;
                    var methodName = NameRefFromTerm(method.NAME());
                    var (parameters, returnType) = VisitFuncSignatureContent(method.funcSignature());
                    var methodTypeParams = VisitTypeParamListContent(method.funcSignature().typeParamList());
                    var cmNode = new ClassMethodNode(methodName, parameters, returnType, [], null, isLocal, isStatic, isAsync, isProtected, isOverride, isAbstract, SpanFromCtx(method));
                    cmNode.TypeParams = methodTypeParams;
                    methods.Add(cmNode);
                    break;
                }
                case LuxParser.DeclareClassConstructorMemberContext ctor:
                {
                    var (parameters, _) = VisitFuncSignatureContent(ctor.funcSignature());
                    constructor = new ClassConstructorNode(parameters, [], null, SpanFromCtx(ctor));
                    break;
                }
                case LuxParser.DeclareClassAccessorMemberContext accessor:
                {
                    var kindName = accessor.NAME(0).GetText();
                    var propName = NameRefFromTerm(accessor.NAME(1));
                    var kind = kindName == "get" ? AccessorKind.Getter : AccessorKind.Setter;
                    var (parameters, returnType) = VisitFuncSignatureContent(accessor.funcSignature());
                    accessors.Add(new ClassAccessorNode(kind, propName, parameters, returnType, [], null, false, SpanFromCtx(accessor)));
                    break;
                }
            }
        }

        var isClassAbstract = context.ABSTRACT() != null;
        var declMod = new ClassDecl(NewNodeID, SpanFromCtx(context), name, baseClass, interfaces, fields, methods, constructor, accessors, isDeclare: true, isAbstract: isClassAbstract);
        declMod.TypeParams = typeParams;
        declMod.BaseClassTypeArgs = baseClassTypeArgs;
        declMod.InterfaceTypeArgs = interfaceTypeArgs;
        return declMod;
    }

    public override Node VisitModuleDeclareInterface(LuxParser.ModuleDeclareInterfaceContext context)
    {
        var name = NameRefFromTerm(context.NAME());
        var classRefs = context.classRef();

        var baseInterfaces = new List<NameRef>();
        var baseInterfaceTypeArgs = new List<List<TypeArgRef>>();
        if (context.EXTENDS() != null)
        {
            foreach (var cr in classRefs)
            {
                var (iName, iArgs) = VisitClassRefContent(cr);
                baseInterfaces.Add(iName);
                baseInterfaceTypeArgs.Add(iArgs);
            }
        }

        var typeParams = VisitTypeParamListContent(context.typeParamList());

        var fields = new List<InterfaceFieldNode>();
        var methods = new List<InterfaceMethodNode>();

        foreach (var member in context.interfaceMember())
        {
            switch (member)
            {
                case LuxParser.InterfaceFieldMemberContext field:
                {
                    var fieldName = NameRefFromTerm(field.NAME());
                    var typeAnn = (TypeRef)Visit(field.typeAnnotation().typeExpr());
                    fields.Add(new InterfaceFieldNode(fieldName, typeAnn, SpanFromCtx(field)));
                    break;
                }
                case LuxParser.InterfaceMethodMemberContext method:
                {
                    var isAsync = method.ASYNC() != null;
                    var methodName = NameRefFromTerm(method.NAME());
                    var (parameters, returnType) = VisitFuncSignatureContent(method.funcSignature());
                    var imTypeParams = VisitTypeParamListContent(method.funcSignature().typeParamList());
                    var imNode = new InterfaceMethodNode(methodName, parameters, returnType, isAsync, SpanFromCtx(method));
                    imNode.TypeParams = imTypeParams;
                    methods.Add(imNode);
                    break;
                }
            }
        }

        var ifaceModDecl = new InterfaceDecl(NewNodeID, SpanFromCtx(context), name, baseInterfaces, fields, methods, isDeclare: true);
        ifaceModDecl.TypeParams = typeParams;
        ifaceModDecl.BaseInterfaceTypeArgs = baseInterfaceTypeArgs;
        return ifaceModDecl;
    }

    public override Node VisitClassDecl(LuxParser.ClassDeclContext context)
    {
        var name = NameRefFromTerm(context.NAME());
        var classRefs = context.classRef();

        NameRef? baseClass = null;
        var baseClassTypeArgs = new List<TypeArgRef>();
        var interfaces = new List<NameRef>();
        var interfaceTypeArgs = new List<List<TypeArgRef>>();

        var refIdx = 0;
        if (context.EXTENDS() != null && classRefs.Length > refIdx)
        {
            var (bcName, bcArgs) = VisitClassRefContent(classRefs[refIdx]);
            baseClass = bcName;
            baseClassTypeArgs = bcArgs;
            refIdx++;
        }

        if (context.IMPLEMENTS() != null)
        {
            for (var i = refIdx; i < classRefs.Length; i++)
            {
                var (iName, iArgs) = VisitClassRefContent(classRefs[i]);
                interfaces.Add(iName);
                interfaceTypeArgs.Add(iArgs);
            }
        }

        var typeParams = VisitTypeParamListContent(context.typeParamList());

        var fields = new List<ClassFieldNode>();
        var methods = new List<ClassMethodNode>();
        ClassConstructorNode? constructor = null;
        var accessors = new List<ClassAccessorNode>();

        foreach (var member in context.classMember())
        {
            switch (member)
            {
                case LuxParser.ClassFieldMemberContext field:
                {
                    var isLocal = field.LOCAL() != null;
                    var isStatic = field.STATIC() != null;
                    var isProtected = field.PROTECTED() != null;
                    var fieldName = NameRefFromTerm(field.NAME());
                    TypeRef? typeAnn = field.typeAnnotation() != null
                        ? (TypeRef)Visit(field.typeAnnotation().typeExpr())
                        : null;
                    Expr? defaultValue = field.expr() != null ? (Expr)Visit(field.expr()) : null;
                    var fieldNode = new ClassFieldNode(fieldName, typeAnn, defaultValue, isLocal, isStatic, isProtected, SpanFromCtx(field));
                    fieldNode.Annotations = VisitAnnotationListContent(field.annotationList());
                    fields.Add(fieldNode);
                    break;
                }
                case LuxParser.ClassMethodMemberContext method:
                {
                    var isLocal = method.LOCAL() != null;
                    var isStatic = method.STATIC() != null;
                    var isAsync = method.ASYNC() != null;
                    var isProtected = method.PROTECTED() != null;
                    var isOverride = method.OVERRIDE() != null;
                    var methodName = NameRefFromTerm(method.NAME());
                    var (parameters, returnType, body, ret) = VisitFuncBodyContent(method.funcBody());
                    var regMethodTypeParams = VisitTypeParamListContent(method.funcBody().typeParamList());
                    var regMethodNode = new ClassMethodNode(methodName, parameters, returnType, body, ret, isLocal, isStatic, isAsync, isProtected, isOverride, false, SpanFromCtx(method));
                    regMethodNode.TypeParams = regMethodTypeParams;
                    regMethodNode.Annotations = VisitAnnotationListContent(method.annotationList());
                    methods.Add(regMethodNode);
                    break;
                }
                case LuxParser.ClassAbstractMethodMemberContext absMethod:
                {
                    var isProtected = absMethod.PROTECTED() != null;
                    var isAsync = absMethod.ASYNC() != null;
                    var methodName = NameRefFromTerm(absMethod.NAME());
                    var (parameters, returnType) = VisitFuncSignatureContent(absMethod.funcSignature());
                    var absMethodTypeParams = VisitTypeParamListContent(absMethod.funcSignature().typeParamList());
                    var absMethodNode = new ClassMethodNode(methodName, parameters, returnType, [], null, false, false, isAsync, isProtected, false, true, SpanFromCtx(absMethod));
                    absMethodNode.TypeParams = absMethodTypeParams;
                    absMethodNode.Annotations = VisitAnnotationListContent(absMethod.annotationList());
                    methods.Add(absMethodNode);
                    break;
                }
                case LuxParser.ClassConstructorMemberContext ctor:
                {
                    var (parameters, _, body, ret) = VisitFuncBodyContent(ctor.funcBody());
                    constructor = new ClassConstructorNode(parameters, body, ret, SpanFromCtx(ctor));
                    constructor.Annotations = VisitAnnotationListContent(ctor.annotationList());
                    break;
                }
                case LuxParser.ClassAccessorMemberContext accessor:
                {
                    var isOverride = accessor.OVERRIDE() != null;
                    var kindName = accessor.NAME(0).GetText();
                    var propName = NameRefFromTerm(accessor.NAME(1));
                    var kind = kindName == "get" ? AccessorKind.Getter : AccessorKind.Setter;
                    var (parameters, returnType, body, ret) = VisitFuncBodyContent(accessor.funcBody());
                    var accNode = new ClassAccessorNode(kind, propName, parameters, returnType, body, ret, isOverride, SpanFromCtx(accessor));
                    accNode.Annotations = VisitAnnotationListContent(accessor.annotationList());
                    accessors.Add(accNode);
                    break;
                }
                case LuxParser.ClassOperatorMemberContext opMember:
                {
                    var (parameters, returnType, body, ret) = VisitFuncBodyContent(opMember.funcBody());
                    var symText = opMember.operatorSymbol().GetText();
                    var metaName = OperatorSymbolToMetamethod(symText, parameters.Count, out var diagMsg);
                    if (metaName == null)
                    {
                        diag.Report(SpanFromCtx(opMember.operatorSymbol()), Diagnostics.DiagnosticCode.ErrInvalidOperator, diagMsg ?? symText);
                        break;
                    }
                    var opNameRef = NameRefFromText(metaName, SpanFromCtx(opMember.operatorSymbol()));
                    var opMethodNode = new ClassMethodNode(
                        opNameRef, parameters, returnType, body, ret,
                        isLocal: false, isStatic: false, isAsync: false,
                        isProtected: false, isOverride: false, isAbstract: false,
                        SpanFromCtx(opMember), isOperator: true, operatorSymbol: symText);
                    opMethodNode.Annotations = VisitAnnotationListContent(opMember.annotationList());
                    methods.Add(opMethodNode);
                    break;
                }
            }
        }

        var isClassAbstract = context.ABSTRACT() != null;
        var regularDecl = new ClassDecl(NewNodeID, SpanFromCtx(context), name, baseClass, interfaces, fields, methods, constructor, accessors, isAbstract: isClassAbstract);
        regularDecl.TypeParams = typeParams;
        regularDecl.BaseClassTypeArgs = baseClassTypeArgs;
        regularDecl.InterfaceTypeArgs = interfaceTypeArgs;
        regularDecl.Annotations = VisitAnnotationListContent(context.annotationList());
        return regularDecl;
    }

    public override Node VisitInterfaceDecl(LuxParser.InterfaceDeclContext context)
    {
        var name = NameRefFromTerm(context.NAME());
        var classRefs = context.classRef();

        var baseInterfaces = new List<NameRef>();
        var baseInterfaceTypeArgs = new List<List<TypeArgRef>>();
        if (context.EXTENDS() != null)
        {
            foreach (var cr in classRefs)
            {
                var (iName, iArgs) = VisitClassRefContent(cr);
                baseInterfaces.Add(iName);
                baseInterfaceTypeArgs.Add(iArgs);
            }
        }

        var typeParams = VisitTypeParamListContent(context.typeParamList());

        var fields = new List<InterfaceFieldNode>();
        var methods = new List<InterfaceMethodNode>();

        foreach (var member in context.interfaceMember())
        {
            switch (member)
            {
                case LuxParser.InterfaceFieldMemberContext field:
                {
                    var fieldName = NameRefFromTerm(field.NAME());
                    var typeAnn = (TypeRef)Visit(field.typeAnnotation().typeExpr());
                    var ifaceField = new InterfaceFieldNode(fieldName, typeAnn, SpanFromCtx(field));
                    ifaceField.Annotations = VisitAnnotationListContent(field.annotationList());
                    fields.Add(ifaceField);
                    break;
                }
                case LuxParser.InterfaceMethodMemberContext method:
                {
                    var isAsync = method.ASYNC() != null;
                    var methodName = NameRefFromTerm(method.NAME());
                    var (parameters, returnType) = VisitFuncSignatureContent(method.funcSignature());
                    var imTypeParams = VisitTypeParamListContent(method.funcSignature().typeParamList());
                    var imNode = new InterfaceMethodNode(methodName, parameters, returnType, isAsync, SpanFromCtx(method));
                    imNode.TypeParams = imTypeParams;
                    imNode.Annotations = VisitAnnotationListContent(method.annotationList());
                    methods.Add(imNode);
                    break;
                }
            }
        }

        var ifaceRegular = new InterfaceDecl(NewNodeID, SpanFromCtx(context), name, baseInterfaces, fields, methods);
        ifaceRegular.TypeParams = typeParams;
        ifaceRegular.BaseInterfaceTypeArgs = baseInterfaceTypeArgs;
        ifaceRegular.Annotations = VisitAnnotationListContent(context.annotationList());
        return ifaceRegular;
    }
}
