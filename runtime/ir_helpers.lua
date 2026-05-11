-- IR helper factories for annotation apply() scripts.
-- Auto-loaded into the sandboxed LuxRuntime by ApplyAnnotationsPass.
-- Returned tables use the same wire format that IRLuaCodec expects when decoding.
-- Discriminator field is "__kind" (chosen to avoid collisions with IR properties
-- that happen to be named "Kind", e.g. on TypeRef).

ir = {}

local function span()
    return { 1, 1, 1, 1 }
end

local function nameRef(name)
    return { name = name, span = span() }
end

function ir.stringLiteral(value)
    return { __kind = "StringLiteralExpr", __span = span(), value = value }
end

function ir.numberLiteral(raw)
    return { __kind = "NumberLiteralExpr", __span = span(), raw = tostring(raw), kind = "Int" }
end

function ir.boolLiteral(value)
    return { __kind = "BoolLiteralExpr", __span = span(), value = value and true or false }
end

function ir.nilLiteral()
    return { __kind = "NilLiteralExpr", __span = span() }
end

function ir.nameExpr(name)
    return { __kind = "NameExpr", __span = span(), name = nameRef(name) }
end

function ir.call(callee, args)
    local calleeNode = type(callee) == "string" and ir.nameExpr(callee) or callee
    return {
        __kind = "FunctionCallExpr",
        __span = span(),
        callee = calleeNode,
        arguments = args or {},
        isOptional = false,
    }
end

function ir.methodCall(object, method, args)
    return {
        __kind = "MethodCallExpr",
        __span = span(),
        object = object,
        methodName = nameRef(method),
        arguments = args or {},
    }
end

function ir.exprStmt(expr)
    return { __kind = "ExprStmt", __span = span(), expression = expr }
end

function ir.returnStmt(values)
    return { __kind = "ReturnStmt", __span = span(), values = values or {} }
end

function ir.dotAccess(object, field)
    return {
        __kind = "DotAccessExpr",
        __span = span(),
        object = object,
        fieldName = nameRef(field),
        isOptional = false,
    }
end

function ir.localDecl(name, value)
    return {
        __kind = "LocalDecl",
        __span = span(),
        variables = { { name = nameRef(name), attribute = nil, typeAnnotation = nil, span = span() } },
        values = value and { value } or {},
        isMutable = true,
    }
end
