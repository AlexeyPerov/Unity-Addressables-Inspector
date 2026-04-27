namespace AddressablesInspector
{
    public abstract class PartDrawerBase : IPartDrawer
    {
        protected AddressablesInspectorWindow Context { get; private set; }
        
        public void SetupContext(AddressablesInspectorWindow context)
        {
            Context = context;
        }

        public virtual void OnSelected()
        {
            
        }

        public abstract void Draw();
    }
}