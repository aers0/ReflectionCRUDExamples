var jsonType = GetEntityType(modelName);
            
var methodInfo = typeof(JObject).GetMethods().LastOrDefault(m => m.Name == nameof(JObject.ToObject) && m.IsGenericMethodDefinition);
            
var genericMethod = methodInfo.MakeGenericMethod(jsonType);
var entity = genericMethod.Invoke(json, null);
         
//And Here we should call our Generic DataContext Method
//For example in this manner:
GetByReflection(this, GetEntityType(modelName).AssemblyQualifiedName, "Remove", _context, entity );
        
        
        
      