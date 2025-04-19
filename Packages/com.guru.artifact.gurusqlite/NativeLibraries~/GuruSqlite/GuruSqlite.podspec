#
# Be sure to run `pod lib lint GuruSqlite.podspec' to ensure this is a
# valid spec before submitting.
#
# Any lines starting with a # are optional, but their use is encouraged
# To learn more about a Podspec see https://guides.cocoapods.org/syntax/podspec.html
#

Pod::Spec.new do |s|
  s.name             = 'GuruSqlite'
  s.version          = '0.1.0'
  s.summary          = 'A short description of GuruSqlite.'

# This description is used to generate tags and improve search results.
#   * Think: What does it do? Why did you write it? What is the focus?
#   * Try to keep it short, snappy and to the point.
#   * Write the description between the DESC delimiters below.
#   * Finally, don't worry about the indent, CocoaPods strips it!

  s.description      = <<-DESC
TODO: Add long description of the pod here.
                       DESC

  s.homepage         = 'https://github.com/castbox/GuruSqlite-iOS'
  # s.screenshots     = 'www.example.com/screenshots_1', 'www.example.com/screenshots_2'
  s.license          = { :type => 'MIT', :file => 'LICENSE' }
  s.author           = { 'Haoyi' => 'haoyi.zhang@castbox.fm' }
  s.source           = { :git => 'https://github.com/castbox/GuruSqlite-iOS.git', :tag => s.version.to_s }
  s.source_files = 'GuruSqlite/Classes/**/*.{h,m}'
  s.public_header_files = 'GuruSqlite/Classes/include/**/*.h'
  s.ios.deployment_target = '12.0'
  s.swift_version = '5'
#   s.pod_target_xcconfig = { 'DEFINES_MODULE' => 'YES', 'EXCLUDED_ARCHS[sdk=iphonesimulator*]' => 'i386' }
  s.resource_bundles = {'guru_sqlite_privacy' => ['GuruSqlite/Classes/Resources/PrivacyInfo.xcprivacy']}


  s.source_files = 'GuruSqlite/Classes/**/*'
  
  # s.resource_bundles = {
  #   'GuruSqlite' => ['GuruSqlite/Assets/*.png']
  # }

  # s.public_header_files = 'Pod/Classes/**/*.h'
  # s.frameworks = 'UIKit', 'MapKit'
  # s.dependency 'AFNetworking', '~> 2.3'
end
