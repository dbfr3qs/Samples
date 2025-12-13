// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "IdpMobileClient",
    platforms: [
        .iOS(.v17)
    ],
    products: [
        .library(
            name: "IdpMobileClient",
            targets: ["IdpMobileClient"]
        )
    ],
    targets: [
        .target(
            name: "IdpMobileClient",
            dependencies: []
        )
    ]
)
