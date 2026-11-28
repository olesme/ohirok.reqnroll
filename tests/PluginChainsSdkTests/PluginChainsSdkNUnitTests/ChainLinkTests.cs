using NUnit.Framework;
using Ohirok.RuntimeMiddleware;
using Reqnroll.BoDi;

namespace PluginChainsSdkNUnitTests;

/// <summary>
/// Chain links on real BoDi containers, shaped like Reqnroll's: a global container and test-thread children.
/// </summary>
public class ChainLinkTests
{
    public interface IService { string Describe(); }

    public sealed class DefaultService : IService { public string Describe() => "default"; }

    /// <summary>A plugin that is not built on the SDK and simply replaces the service (like ReportPortal does).</summary>
    public sealed class ThirdPartyService : IService { public string Describe() => "third-party"; }

    public sealed class LinkA(IService inner) : IService { public string Describe() => "A(" + inner.Describe() + ")"; }
    public sealed class LinkB(IService inner) : IService { public string Describe() => "B(" + inner.Describe() + ")"; }

    static (ObjectContainer global, ObjectContainer thread) Containers()
    {
        var global = new ObjectContainer();
        global.RegisterTypeAs<DefaultService, IService>();
        return (global, new ObjectContainer(global));
    }

    [Test]
    public void Chain_in_child_wraps_the_parents_implementation()
    {
        var (_, thread) = Containers();
        thread.RegisterChainLink<IService, DefaultService, LinkA>();

        Assert.That(thread.Resolve<IService>().Describe(), Is.EqualTo("A(default)"));
    }

    [Test]
    public void Chain_wraps_a_third_party_replacement_registered_before_the_links()
    {
        var (global, thread) = Containers();
        global.RegisterTypeAs<ThirdPartyService, IService>();
        thread.RegisterChainLink<IService, DefaultService, LinkA>();

        Assert.That(thread.Resolve<IService>().Describe(), Is.EqualTo("A(third-party)"));
    }

    [Test]
    public void Chain_wraps_a_third_party_replacement_registered_after_the_links()
    {
        // Plugin load order decides when a plain replacement lands in the global container; it must not matter.
        var (global, thread) = Containers();
        thread.RegisterChainLink<IService, DefaultService, LinkA>();
        global.RegisterTypeAs<ThirdPartyService, IService>();

        Assert.That(thread.Resolve<IService>().Describe(), Is.EqualTo("A(third-party)"));
    }

    [Test]
    public void Higher_order_is_closer_to_the_base_regardless_of_registration_order()
    {
        var (_, thread) = Containers();
        thread.RegisterChainLink<IService, DefaultService, LinkA>(order: 10);
        thread.RegisterChainLink<IService, DefaultService, LinkB>(order: 20);

        Assert.That(thread.Resolve<IService>().Describe(), Is.EqualTo("A(B(default))"));
    }

    [Test]
    public void Each_child_container_gets_its_own_chain()
    {
        var (global, thread1) = Containers();
        var thread2 = new ObjectContainer(global);
        thread1.RegisterChainLink<IService, DefaultService, LinkA>();
        thread2.RegisterChainLink<IService, DefaultService, LinkB>();

        Assert.Multiple(() =>
        {
            Assert.That(thread1.Resolve<IService>().Describe(), Is.EqualTo("A(default)"));
            Assert.That(thread2.Resolve<IService>().Describe(), Is.EqualTo("B(default)"));
            Assert.That(global.Resolve<IService>().Describe(), Is.EqualTo("default"), "the parent is not changed");
        });
    }

    [Test]
    public void Same_registrations_in_two_children_build_separate_instances()
    {
        var (global, thread1) = Containers();
        var thread2 = new ObjectContainer(global);
        thread1.RegisterChainLink<IService, DefaultService, LinkA>();
        thread2.RegisterChainLink<IService, DefaultService, LinkA>();

        Assert.That(thread1.Resolve<IService>(), Is.Not.SameAs(thread2.Resolve<IService>()));
    }

    [Test]
    public void Chain_is_built_once_per_container()
    {
        var (_, thread) = Containers();
        thread.RegisterChainLink<IService, DefaultService, LinkA>();

        Assert.That(thread.Resolve<IService>(), Is.SameAs(thread.Resolve<IService>()));
    }

    [Test]
    public void Chain_in_a_top_level_container_starts_from_TBase()
    {
        var global = new ObjectContainer();
        global.RegisterChainLink<IService, DefaultService, LinkA>();

        Assert.That(global.Resolve<IService>().Describe(), Is.EqualTo("A(default)"));
    }

    [Test]
    public void Replace_keeps_one_link_of_a_type_Allow_adds_it_again_Ignore_keeps_the_first()
    {
        var (_, replace) = Containers();
        replace.RegisterChainLink<IService, DefaultService, LinkA>(order: 1);
        replace.RegisterChainLink<IService, DefaultService, LinkA>(order: 2);

        var (_, allow) = Containers();
        allow.AddChainLink<IService, DefaultService, LinkA>();
        allow.AddChainLink<IService, DefaultService, LinkA>();

        var (_, ignore) = Containers();
        ignore.RegisterChainLink<IService, DefaultService, LinkA>(order: 10);
        ignore.RegisterChainLink<IService, DefaultService, LinkB>(order: 20);
        ignore.RegisterChainLinkIfNotExists<IService, DefaultService, LinkA>(order: 30);

        Assert.Multiple(() =>
        {
            Assert.That(replace.Resolve<IService>().Describe(), Is.EqualTo("A(default)"));
            Assert.That(allow.Resolve<IService>().Describe(), Is.EqualTo("A(A(default))"));
            Assert.That(ignore.Resolve<IService>().Describe(), Is.EqualTo("A(B(default))"), "the order-10 link stays");
        });
    }

    [Test]
    public void When_predicate_skips_the_link()
    {
        var (_, thread) = Containers();
        thread.RegisterChainLink<IService, DefaultService, LinkA>(when: _ => false);
        thread.RegisterChainLink<IService, DefaultService, LinkB>();

        Assert.That(thread.Resolve<IService>().Describe(), Is.EqualTo("B(default)"));
    }
}
