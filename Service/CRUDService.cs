namespace API.Services
{
    public class CRUDService
    {
        private readonly ApplicationContext _context;

        public ManageService(ApplicationContext context)
        {
            _context = context;
        }
        
        //It's a entry point, from here everything is begin :)
        public async Task<object> GetResult(string entityType, string method, JObject json)
        {
            response = method switch
                {
                    "delete" => await Call(entityType, json, "Remove"),
                    "edit" => await Call(entityType, json, "Update"),
                    "list" => await InvokeByReflection(GetModelType(entityType).AssemblyQualifiedName, "List"),
                    _ => response
                };
            return response;
        }
        
        async Task<List<T>> List<T>() where T : class
        {
            var query =  _context.Set<T>().AsQueryable();
            var navigations = GetNavigationProperties<T>();

            foreach (var property in navigations)
                query = query.Include(property.Name);
            return await query.ToListAsync();
        }
        
        async Task<uint> Remove<T>(T entity) where T : class
        {
            PropertyInfo prop = typeof(T).GetProperty("Id");

            var Id = (uint) prop.GetValue(entity) ;
            
            var e =  await _context.Set<T>().FindAsync(Id);
            
            _context.Remove(e);
            
             await _context.SaveChangesAsync();
             return Id;
        }
        
        async Task<T> Update<T>(T entity) where T : class
        {
            PropertyInfo prop = typeof(T).GetProperty("Id");
            
            var Id = (uint) prop.GetValue(entity) ;

            if (Id == 0)
            {
               var newEntity = (T)Activator.CreateInstance(typeof(T));

                _context.Add(newEntity);
                _context.SaveChanges();

                Id = (uint) prop.GetValue(newEntity) ;
            }
            
            var query = _context.Set<T>().AsQueryable();
            
            var navProperties = GetNavigationProperties<T>();
            
            foreach (var property in  navProperties)
            {
                var propertyName = property.Name;
                if (entity.GetType().GetProperty(propertyName).GetValue(entity) == null) continue;
                query = query.Include(property.Name);
            }
            
            var e =query.Single(b => EF.Property<uint>(b, "Id") == Id);

            //Clear every Navigation Property in entity from DB
            foreach (var property in  navProperties)
            {
                var p = property.PropertyInfo;
               
              var incomingEntityVal = p.GetValue(entity);
              if (incomingEntityVal == null) continue;
               
               var m = p.PropertyType.GetMethod("Clear");
                
               if(m != null)
                m.Invoke(p.GetValue(e, null), new object[] {  });

            }
            _context.SaveChanges(); // Do not async, need result immediately
            _context.ChangeTracker.Clear(); // Do not forget clean tracker before Write Navigation Properties

            WriteProperties(entity,  e);
            
            _context.Update(e).Property("Id").IsModified = false; // Id is untrackable
            await _context.SaveChangesAsync();

            return e;
        }
        
        #region Helpers
        async Task<object> Call (string modelName, JObject json, string methodName)
        {
            var jsonType = GetModelType(modelName);
            
            var methodInfo = typeof(JObject).GetMethods().LastOrDefault(m => m.Name == nameof(JObject.ToObject) && m.IsGenericMethodDefinition);
            
            var genericMethod = methodInfo.MakeGenericMethod(jsonType);
            var entity = genericMethod.Invoke(json, null);
              
            return await InvokeByReflection(jsonType.AssemblyQualifiedName, methodName,  entity );
        }
        Task<object> InvokeByReflection(string modelName, string methodName, params object[] param)
        {
            var method = typeof(ManageService).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            var generic = method.MakeGenericMethod(Type.GetType(modelName));
            return generic.InvokeAsync(this, param);
        }
        void WriteProperties(object values, object entity)
        {
            foreach (var property in values.GetType().GetProperties().Where(p => p.CanWrite))
            {
                var val = property.GetValue(values);
                if (val == null) continue;
                
                property.SetValue(entity, val );
            }
        }
        
        Type GetModelType(string modelName)
        {
            return Assembly.GetExecutingAssembly().GetReferencedAssemblies()
                .SelectMany(x => Assembly.Load(x.Name).GetTypes())
                .FirstOrDefault(x => x.FullName.Contains("Domain.Models") && (x.Name == (modelName.ToTitleCase())));
        }
        
        IEnumerable<INavigationBase> GetNavigationProperties<T>() where T : class
        {
            return _context.Model.FindEntityType(typeof(T))
                .GetDerivedTypesInclusive()
                .SelectMany(type => type.GetNavigations().Concat<INavigationBase>(type.GetSkipNavigations()))
                .Distinct();
        }
        #endregion Helpers

    }
}
