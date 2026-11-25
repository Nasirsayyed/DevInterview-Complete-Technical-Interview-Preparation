# kotlinx.serialization: keep generated serializers for @Serializable models and navigation routes.
-keepattributes *Annotation*, InnerClasses, Signature, Exceptions
-keepclassmembers class **$$serializer { *; }
-keepclasseswithmembers class com.devinterview.** { kotlinx.serialization.KSerializer serializer(...); }
-keep,includedescriptorclasses class com.devinterview.**$$serializer { *; }
-keepclassmembers class com.devinterview.** { *** Companion; }
# Retrofit interfaces use generic signatures and annotations at runtime.
-keep,allowobfuscation interface com.devinterview.core.network.** { *; }
-dontwarn org.conscrypt.**
-dontwarn org.bouncycastle.**
-dontwarn org.openjsse.**
