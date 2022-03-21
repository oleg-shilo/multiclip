using System;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace MultiClip.UI
{
    /// <summary>
    /// Extremely simplistic Claiburn.Micro binder replacement. Deployment pressure is too strong to justify 
    /// introducing Caliburn and other decencies 
    /// </summary>
    static class AutoBinder
    {
        public static void BindOnLoad(FrameworkElement element, object model)
        {
            //Note: Trying to call Bind for Window may yield 0 visual children
            //if you it is called from the constructor (even after InitializeComponent()).
            //This is because the visual children aren't loaded yet.

            if (element.IsLoaded)
                Bind((DependencyObject)element, model); //typecast to avoid re-entrance
            else
                element.Loaded += (s, e) => BindOnLoad(element, model);
        }

        public static void Bind(DependencyObject element, object model)
        {
            if (element != null)
            {
                if (element is FrameworkElement)
                    (element as FrameworkElement).DataContext = model;

                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(element, i);
                    if (child != null && child is FrameworkElement)
                        BindElement((FrameworkElement)child, model);
                    Bind(child, model);
                }
            }
        }

        public static void BindElement(FrameworkElement element, object model)
        {
            if (string.IsNullOrEmpty(element.Name))
                return;

            MemberInfo modelMember = model.GetType().GetMember(element.Name).FirstOrDefault();

            if (modelMember == null)
                return;

            MemberInfo modelPropEnabled = model.GetType()
                                               .GetMembers()
                                               .OfType<PropertyInfo>()
                                               .Where(x => x.PropertyType == typeof(bool) && (x.Name == "Can" + element.Name || x.Name == element.Name + "Enabled"))
                                               .FirstOrDefault();

            string singularName = element.Name.TrimEnd('s');

            MemberInfo modelPropSelected = model.GetType()
                                                .GetMembers()
                                                .OfType<PropertyInfo>()
                                                .Where(x => (x.Name == "Current" + singularName || x.Name == "Selected" + singularName))
                                                .FirstOrDefault();

            var modelMethod = modelMember as MethodInfo;
            var modelProp = modelMember as PropertyInfo;

            if (element is ItemsControl)
            {
                var selector = element as Selector;
                var control = element as ItemsControl;
                var prop = ItemsControl.ItemsSourceProperty;

                if (modelProp != null && element.GetBindingExpression(prop) == null)
                    control.SetBinding(prop, new Binding(modelProp.Name) { Source = model });

                if (selector != null && modelPropSelected != null && element.GetBindingExpression(Selector.SelectedItemProperty) == null)
                    control.SetBinding(Selector.SelectedItemProperty, new Binding(modelPropSelected.Name) { Source = model, Mode = BindingMode.TwoWay });

                if (modelPropEnabled != null && element.GetBindingExpression(UIElement.IsEnabledProperty) == null)
                    control.SetBinding(UIElement.IsEnabledProperty, new Binding(modelPropEnabled.Name) { Source = model });
            }

            else if (element is TextBlock)
            {
                var control = element as TextBlock;
                var prop = TextBlock.TextProperty;

                if (modelProp != null && element.GetBindingExpression(prop) == null)
                    control.SetBinding(prop, new Binding(modelProp.Name) { Source = model });

                if (modelPropEnabled != null && element.GetBindingExpression(UIElement.IsEnabledProperty) == null)
                    control.SetBinding(UIElement.IsEnabledProperty, new Binding(modelPropEnabled.Name) { Source = model });
            }
            else if (element is TextBox)
            {
                var control = element as TextBox;
                var prop = TextBox.TextProperty;

                if (modelProp != null && element.GetBindingExpression(prop) == null)
                    control.SetBinding(prop, new Binding(modelProp.Name) { Source = model, Mode = BindingMode.TwoWay });

                if (modelPropEnabled != null && element.GetBindingExpression(UIElement.IsEnabledProperty) == null)
                    control.SetBinding(UIElement.IsEnabledProperty, new Binding(modelPropEnabled.Name) { Source = model, Mode = BindingMode.OneWay});
            }
            else if (element is CheckBox)
            {
                var control = element as CheckBox;
                var prop = CheckBox.IsCheckedProperty;

                if (modelProp != null && element.GetBindingExpression(prop) == null)
                    control.SetBinding(prop, new Binding(modelProp.Name) { Source = model, Mode = BindingMode.TwoWay });

                if (modelPropEnabled != null && element.GetBindingExpression(UIElement.IsEnabledProperty) == null)
                    control.SetBinding(UIElement.IsEnabledProperty, new Binding(modelPropEnabled.Name) { Source = model });
            }
            else if (element is RadioButton)
            {
                var control = element as RadioButton;
                var prop = RadioButton.IsCheckedProperty;

                if (modelProp != null && element.GetBindingExpression(prop) == null)
                    control.SetBinding(prop, new Binding(modelProp.Name) { Source = model, Mode = BindingMode.TwoWay });

                if (modelPropEnabled != null && element.GetBindingExpression(UIElement.IsEnabledProperty) == null)
                    control.SetBinding(UIElement.IsEnabledProperty, new Binding(modelPropEnabled.Name) { Source = model });
            }
            else if (element is ButtonBase) //important to have it as the last 'if clause' as otherwise it will collect all check boxes and radio buttons
            {
                var control = element as ButtonBase;

                if (modelMethod != null)
                {
                    ParameterInfo[] paramsInfo = modelMethod.GetParameters();

                    control.Click += (sender, e) =>
                    {
                        object[] @params = new object[paramsInfo.Length];

                        for (int i = 0; i < paramsInfo.Length; i++)
                        {
                            var info = paramsInfo[i];

                            if (info.ParameterType.IsAssignableFrom(sender.GetType()) && info.Name.SameAs("sender", ignoreCase:true))
                                @params[i] = sender;
                            else  if (info.ParameterType.IsAssignableFrom(e.GetType()))
                                @params[i] = e;
                        }

                        modelMethod.Invoke(model, @params);
                    };
                }

                if (modelPropEnabled != null && element.GetBindingExpression(UIElement.IsEnabledProperty) == null)
                    control.SetBinding(UIElement.IsEnabledProperty, new Binding(modelPropEnabled.Name) { Source = model, Mode = BindingMode.TwoWay });
            }
        }
    }

    public class NotifyPropertyChangedBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged<T>(Expression<Func<T>> expression)
        {
            if (PropertyChanged != null)
                this.InUIThread(() => PropertyChanged(this, new PropertyChangedEventArgs(Reflect.NameOf(expression))));
        }
    }

    public static class Reflect
    {
        public static void InUIThread(this object obj, int withDelay, System.Action action)
        {
            Task.Factory.StartNew(() =>
            {
                Thread.Sleep(withDelay);
                obj.InUIThread(action);
            });
        }

        public static void InUIThread(this object obj, System.Action action)
        {
            if (Application.Current != null) //to handle exit
            {
                if (Application.Current.Dispatcher.CheckAccess())
                    action();
                else
                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, action);
            }
        }
        /// <summary>
        /// Gets the Member name of the lambda expression.
        /// <para> For example "()=>FileName" will return string "FileName".</para>
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <returns></returns>
        public static string GetMemberName(System.Linq.Expressions.Expression expression)
        {
            switch (expression.NodeType)
            {
                case ExpressionType.MemberAccess:
                    var memberExpression = (MemberExpression)expression;

                    string supername = null;
                    if (memberExpression.Expression != null)
                        supername = GetMemberName(memberExpression.Expression);

                    if (String.IsNullOrEmpty(supername))
                        return memberExpression.Member.Name;

                    return String.Concat(supername, '.', memberExpression.Member.Name);

                case ExpressionType.Call:
                    var callExpression = (MethodCallExpression)expression;
                    return callExpression.Method.Name;

                case ExpressionType.Convert:
                    var unaryExpression = (UnaryExpression)expression;
                    return GetMemberName(unaryExpression.Operand);

                case ExpressionType.Constant:
                case ExpressionType.Parameter:
                    return "";

                default:
                    throw new ArgumentException("The expression is not a member access or method call expression");
            }
        }

        /// <summary>
        /// Gets the Member name of the lambda expression.
        /// <para> For example "()=>FileName" will return string "FileName".</para>
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <returns></returns>
        public static string NameOf<T>(Expression<Func<T>> expression)
        {
            return GetMemberName(expression.Body).Split('.').Last();
        }
    }
}