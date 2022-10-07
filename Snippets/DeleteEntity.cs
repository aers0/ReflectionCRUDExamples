var t = entity.GetType();

PropertyInfo prop = t.GetProperty("Id");

var Id = (uint) prop.GetValue(entity) ;
            
_context.Remove(_context.Set<T>().Single(b => EF.Property<uint>(b, "Id") == Id ));
            
await context.SaveChangesAsync();
return Id;