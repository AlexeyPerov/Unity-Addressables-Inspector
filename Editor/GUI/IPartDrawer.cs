namespace AddressablesInspector
{
    public interface IPartDrawer
    {
        void SetupContext(AddressablesInspectorWindow context);
        void OnSelected();
        void Draw();
    }
}