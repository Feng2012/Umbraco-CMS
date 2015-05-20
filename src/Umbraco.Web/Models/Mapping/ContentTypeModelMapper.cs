﻿using System;
using AutoMapper;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Mapping;
using Umbraco.Core.PropertyEditors;
using Umbraco.Web.Models.ContentEditing;

namespace Umbraco.Web.Models.Mapping
{
    /// <summary>
    /// Defines mappings for content/media/members type mappings
    /// </summary>
    internal class ContentTypeModelMapper : MapperConfiguration
    {
        private readonly Lazy<PropertyEditorResolver> _propertyEditorResolver;
        
        //default ctor
        public ContentTypeModelMapper()
        {
            _propertyEditorResolver = new Lazy<PropertyEditorResolver>(() => PropertyEditorResolver.Current);
        }

        //ctor can be used for testing
        public ContentTypeModelMapper(Lazy<PropertyEditorResolver> propertyEditorResolver)
        {
            _propertyEditorResolver = propertyEditorResolver;
        }

        public override void ConfigureMappings(IConfiguration config, ApplicationContext applicationContext)
        {
            config.CreateMap<IMediaType, ContentTypeBasic>();
            config.CreateMap<IContentType, ContentTypeBasic>();
            config.CreateMap<IMemberType, ContentTypeBasic>();


            config.CreateMap<ContentTypeDisplay, IContentType>()
                .ConstructUsing((ContentTypeDisplay source) => new ContentType(source.ParentId))
                //Ignore these since they are 'read only' and only updated with the repository layer
                .ForMember(dto => dto.CreatorId, expression => expression.Ignore())
                .ForMember(dto => dto.Level, expression => expression.Ignore())
                .ForMember(dto => dto.CreateDate, expression => expression.Ignore())
                .ForMember(dto => dto.UpdateDate, expression => expression.Ignore())
                .ForMember(dto => dto.SortOrder, expression => expression.Ignore())
                .ForMember(dto => dto.IsContainer, expression => expression.MapFrom(display => display.EnableListView))
                //ignore, we'll do this in after map
                .ForMember(dto => dto.PropertyGroups, expression => expression.Ignore())
                .AfterMap((source, dest) =>
                {
                    dest.PropertyGroups = new PropertyGroupCollection();
                    foreach (var groupDisplay in source.Groups)
                    {
                        dest.PropertyGroups.Add(Mapper.Map<PropertyGroup>(groupDisplay));
                    }
                });
            
            config.CreateMap<IContentType, ContentTypeDisplay>()
                //Ignore because this is not actually used for content types
                .ForMember(display => display.Trashed, expression => expression.Ignore())

                .ForMember(
                    dto => dto.AvailableCompositeContentTypes,
                    expression => expression.ResolveUsing(new AvailableCompositeContentTypesResolver(applicationContext)))
                
              
                .ForMember(display => display.EnableListView, expression => expression.MapFrom(type => type.IsContainer))

                .ForMember(
                    dto => dto.Groups,
                    expression => expression.ResolveUsing(new PropertyTypeGroupResolver(applicationContext, _propertyEditorResolver)));

            config.CreateMap<PropertyTypeGroupDisplay, PropertyGroup>()
                .ForMember(g => g.CreateDate, expression => expression.Ignore())                
                .ForMember(g => g.UpdateDate, expression => expression.Ignore())  
                .ForMember(g => g.ParentId, expression => expression.MapFrom(display => display.ParentGroupId))
                
                //NOTE: We don't actually need to map these because auto-mapper will automatically do that since they are the same name!
                //.ForMember(g => g.SortOrder, expression => expression.MapFrom(display => display.SortOrder))
                //.ForMember(g => g.Id, expression => expression.MapFrom(display => display.Id))
                //.ForMember(g => g.Name, expression => expression.MapFrom(display => display.Name))
                
                //ignore these, we'll do this in after map
                .ForMember(g => g.PropertyTypes, expression => expression.Ignore())
                .AfterMap((source, destination) =>
                {
                    destination.PropertyTypes = new PropertyTypeCollection();
                    foreach (var propertyTypeDisplay in source.Properties)
                    {
                        destination.PropertyTypes.Add(Mapper.Map<PropertyType>(propertyTypeDisplay));
                    }
                });


            config.CreateMap<PropertyTypeDisplay, PropertyType>()
                .ConstructUsing((PropertyTypeDisplay propertyTypeDisplay) =>
                {
                    var dataType = applicationContext.Services.DataTypeService.GetDataTypeDefinitionById(propertyTypeDisplay.DataTypeId);
                    if (dataType == null) throw new NullReferenceException("No data type found with id " + propertyTypeDisplay.DataTypeId);
                    return new PropertyType(dataType, propertyTypeDisplay.Alias);
                })
                //ignore because this is set in the ctor
                .ForMember(type => type.Alias, expression => expression.Ignore())
                //ignore because this is obsolete and shouldn't be used
                .ForMember(type => type.DataTypeId, expression => expression.Ignore())
                //ignore because these are 'readonly'
                .ForMember(type => type.CreateDate, expression => expression.Ignore())
                .ForMember(type => type.UpdateDate, expression => expression.Ignore())
                .ForMember(type => type.Mandatory, expression => expression.MapFrom(display => display.Validation.Mandatory))
                .ForMember(type => type.ValidationRegExp, expression => expression.MapFrom(display => display.Validation.Pattern))
                .ForMember(type => type.PropertyEditorAlias, expression => expression.MapFrom(display => display.Editor))                
                .ForMember(type => type.DataTypeDefinitionId, expression => expression.MapFrom(display => display.DataTypeId));
        }

        
    }
}