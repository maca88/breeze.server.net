﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;

using System.Web.Http.OData.Builder;
using Foo;
using Microsoft.Data.Edm;
using Models.NorthwindIB.CF;
using System.Web.Http.OData.Batch;
using Inheritance.Models;

using ProduceTPH;


namespace Foo {
    internal class NorthwindIBContext_Aliased : Models.NorthwindIB.CF.NorthwindIBContext_CF {}
}


namespace Test.WebApi2.OData {
  public static class WebApiConfig {
    public static void Register(HttpConfiguration config) {

      config.Routes.MapODataRoute(
              routeName: "NorthwindIB_ODATA",
              routePrefix: "NorthwindIB_odata",
              model: EdmBuilder.GetEdm<NorthwindIBContext_Aliased>(),
              batchHandler: new DefaultODataBatchHandler(GlobalConfiguration.DefaultServer)
              );

      
      
      var billingModel = EdmBuilder.GetEdm<InheritanceContext>();
      config.Routes.MapODataRoute(
        routeName: "BillingInheritance_ODATA",
        routePrefix: "BillingInheritance_odata",
        model: billingModel,
        batchHandler: new DefaultODataBatchHandler(GlobalConfiguration.DefaultServer)
        );

      var produceModel = EdmBuilder.GetEdm<ProduceTPHContext>();
      config.Routes.MapODataRoute(
        routeName: "ProduceInheritance_ODATA",
        routePrefix: "ProduceInheritance_odata",
        model: produceModel,
        batchHandler: new DefaultODataBatchHandler(GlobalConfiguration.DefaultServer)
        );

      // If using ODataConventionModelBuilder
      ODataConventionModelBuilder builder = new ODataConventionModelBuilder();
      builder.EntitySet<Product>("Products");
      builder.EntitySet<Customer>("Customers");
      builder.EntitySet<Order>("Order");
      builder.EntitySet<Category>("Categories");
      builder.EntitySet<Supplier>("Suppliers");
      
      config.Routes.MapODataRoute("odata", "odata", builder.GetEdmModel());       


      // From Original template

      //// Web API configuration and services

      //// Web API routes
      //config.MapHttpAttributeRoutes();

      //config.Routes.MapHttpRoute(
      //    name: "DefaultApi",
      //    routeTemplate: "api/{controller}/{id}",
      //    defaults: new { id = RouteParameter.Optional }
      //);
    }
  }
}
